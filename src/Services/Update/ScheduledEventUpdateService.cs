using System.Text.Json.Nodes;
using Boyfriend.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Services.Update;

public sealed class ScheduledEventUpdateService : BackgroundService
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;
    private readonly GuildDataService _guildData;
    private readonly ILogger<ScheduledEventUpdateService> _logger;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public ScheduledEventUpdateService(IDiscordRestChannelAPI channelApi, IDiscordRestGuildScheduledEventAPI eventApi,
        GuildDataService guildData, ILogger<ScheduledEventUpdateService> logger, IDiscordRestUserAPI userApi,
        UtilityService utility)
    {
        _channelApi = channelApi;
        _eventApi = eventApi;
        _guildData = guildData;
        _logger = logger;
        _userApi = userApi;
        _utility = utility;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var tasks = new List<Task>();

        while (await timer.WaitForNextTickAsync(ct))
        {
            var guildIds = _guildData.GetGuildIds();

            tasks.AddRange(guildIds.Select(async id =>
            {
                var tickResult = await TickScheduledEventsAsync(id, ct);
                _logger.LogResult(tickResult, $"Error in scheduled events update for guild {id}.");
            }));

            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    private async Task<Result> TickScheduledEventsAsync(Snowflake guildId, CancellationToken ct)
    {
        var failedResults = new List<Result>();
        var data = await _guildData.GetData(guildId, ct);
        var eventsResult = await _eventApi.ListScheduledEventsForGuildAsync(guildId, ct: ct);
        if (!eventsResult.IsDefined(out var events))
        {
            return Result.FromError(eventsResult);
        }

        foreach (var scheduledEvent in events)
        {
            if (!data.ScheduledEvents.ContainsKey(scheduledEvent.ID.Value))
            {
                data.ScheduledEvents.Add(scheduledEvent.ID.Value, new ScheduledEventData(null));
            }

            var storedEvent = data.ScheduledEvents[scheduledEvent.ID.Value];
            if (storedEvent.Status == scheduledEvent.Status)
            {
                var tickResult = await TickScheduledEventAsync(guildId, data, scheduledEvent, storedEvent, ct);
                failedResults.AddIfFailed(tickResult);
                continue;
            }

            var statusChangedResponseResult = scheduledEvent.Status switch
            {
                GuildScheduledEventStatus.Scheduled =>
                    await SendScheduledEventCreatedMessage(scheduledEvent, data.Settings, ct),
                GuildScheduledEventStatus.Active or GuildScheduledEventStatus.Completed =>
                    await SendScheduledEventUpdatedMessage(scheduledEvent, data, ct),
                _ => new ArgumentOutOfRangeError(nameof(scheduledEvent.Status))
            };
            if (statusChangedResponseResult.IsSuccess)
            {
                storedEvent.Status = scheduledEvent.Status;
            }

            failedResults.AddIfFailed(statusChangedResponseResult);
        }

        return failedResults.AggregateErrors();
    }

    private async Task<Result> TickScheduledEventAsync(
        Snowflake guildId, GuildData data, IGuildScheduledEvent scheduledEvent, ScheduledEventData eventData,
        CancellationToken ct)
    {
        if (GuildSettings.AutoStartEvents.Get(data.Settings)
            && DateTimeOffset.UtcNow >= scheduledEvent.ScheduledStartTime
            && scheduledEvent.Status is not GuildScheduledEventStatus.Active)
        {
            return await AutoStartEventAsync(guildId, scheduledEvent, ct);
        }

        var offset = GuildSettings.EventEarlyNotificationOffset.Get(data.Settings);
        if (offset == TimeSpan.Zero
            || eventData.EarlyNotificationSent
            || DateTimeOffset.UtcNow < scheduledEvent.ScheduledStartTime - offset)
        {
            return Result.FromSuccess();
        }

        var sendResult = await SendEarlyEventNotificationAsync(scheduledEvent, data, ct);
        if (sendResult.IsSuccess)
        {
            eventData.EarlyNotificationSent = true;
        }

        return sendResult;
    }

    private async Task<Result> AutoStartEventAsync(
        Snowflake guildId, IGuildScheduledEvent scheduledEvent, CancellationToken ct)
    {
        return (Result)await _eventApi.ModifyGuildScheduledEventAsync(
            guildId, scheduledEvent.ID,
            status: GuildScheduledEventStatus.Active, ct: ct);
    }

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildSettings.EventNotificationRole" /> if one is
    ///     set,
    ///     when a scheduled event is created
    ///     in a guild's <see cref="GuildSettings.EventNotificationChannel" /> if one is set.
    /// </summary>
    /// <param name="scheduledEvent">The scheduled event that has just been created.</param>
    /// <param name="settings">The settings of the guild containing the scheduled event.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A notification sending result which may or may not have succeeded.</returns>
    private async Task<Result> SendScheduledEventCreatedMessage(
        IGuildScheduledEvent scheduledEvent, JsonNode settings, CancellationToken ct = default)
    {
        if (!scheduledEvent.Creator.IsDefined(out var creator))
        {
            return new ArgumentNullError(nameof(scheduledEvent.Creator));
        }

        var eventDescription = scheduledEvent.Description.IsDefined(out var description)
            ? description
            : string.Empty;
        var embedDescriptionResult = scheduledEvent.EntityType switch
        {
            GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice =>
                GetLocalEventCreatedEmbedDescription(scheduledEvent, eventDescription),
            GuildScheduledEventEntityType.External => GetExternalScheduledEventCreatedEmbedDescription(
                scheduledEvent, eventDescription),
            _ => new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType))
        };

        if (!embedDescriptionResult.IsDefined(out var embedDescription))
        {
            return Result.FromError(embedDescriptionResult);
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCreatedTitle, creator.GetTag()), creator)
            .WithTitle(scheduledEvent.Name)
            .WithDescription(embedDescription)
            .WithEventCover(scheduledEvent.ID, scheduledEvent.Image)
            .WithCurrentTimestamp()
            .WithColour(ColorsList.White)
            .Build();
        if (!embed.IsDefined(out var built))
        {
            return Result.FromError(embed);
        }

        var roleMention = !GuildSettings.EventNotificationRole.Get(settings).Empty()
            ? Mention.Role(GuildSettings.EventNotificationRole.Get(settings))
            : string.Empty;

        var button = new ButtonComponent(
            ButtonComponentStyle.Primary,
            Messages.EventDetailsButton,
            new PartialEmoji(Name: "📋"),
            CustomIDHelpers.CreateButtonIDWithState(
                "scheduled-event-details", $"{scheduledEvent.GuildID}:{scheduledEvent.ID}")
        );

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(settings), roleMention, embeds: new[] { built },
            components: new[] { new ActionRowComponent(new[] { button }) }, ct: ct);
    }

    private static Result<string> GetExternalScheduledEventCreatedEmbedDescription(
        IGuildScheduledEvent scheduledEvent, string eventDescription)
    {
        var dataResult = scheduledEvent.TryGetExternalEventData(out var endTime, out var location);
        if (!dataResult.IsSuccess)
        {
            return Result<string>.FromError(dataResult);
        }

        return $"{eventDescription}\n\n{Markdown.BlockQuote(
            string.Format(
                Messages.DescriptionExternalEventCreated,
                Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                Markdown.Timestamp(endTime),
                Markdown.InlineCode(location ?? string.Empty)
            ))}";
    }

    private static Result<string> GetLocalEventCreatedEmbedDescription(
        IGuildScheduledEvent scheduledEvent, string eventDescription)
    {
        if (scheduledEvent.ChannelID is null)
        {
            return new ArgumentNullError(nameof(scheduledEvent.ChannelID));
        }

        return $"{eventDescription}\n\n{Markdown.BlockQuote(
            string.Format(
                Messages.DescriptionLocalEventCreated,
                Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                Mention.Channel(scheduledEvent.ChannelID.Value)
            ))}";
    }

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildSettings.EventNotificationRole" /> and event
    ///     subscribers,
    ///     when a scheduled event has started or completed
    ///     in a guild's <see cref="GuildSettings.EventNotificationChannel" /> if one is set.
    /// </summary>
    /// <param name="scheduledEvent">The scheduled event that is about to start, has started or completed.</param>
    /// <param name="data">The data for the guild containing the scheduled event.</param>
    /// <param name="ct">The cancellation token for this operation</param>
    /// <returns>A reminder/notification sending result which may or may not have succeeded.</returns>
    private async Task<Result> SendScheduledEventUpdatedMessage(
        IGuildScheduledEvent scheduledEvent, GuildData data, CancellationToken ct = default)
    {
        if (scheduledEvent.Status == GuildScheduledEventStatus.Active)
        {
            data.ScheduledEvents[scheduledEvent.ID.Value].ActualStartTime = DateTimeOffset.UtcNow;

            var embedDescriptionResult = scheduledEvent.EntityType switch
            {
                GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice =>
                    GetLocalEventStartedEmbedDescription(scheduledEvent),
                GuildScheduledEventEntityType.External => GetExternalEventStartedEmbedDescription(scheduledEvent),
                _ => new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType))
            };

            var contentResult = await _utility.GetEventNotificationMentions(
                scheduledEvent, data.Settings, ct);
            if (!contentResult.IsDefined(out var content))
            {
                return Result.FromError(contentResult);
            }

            if (!embedDescriptionResult.IsDefined(out var embedDescription))
            {
                return Result.FromError(embedDescriptionResult);
            }

            var startedEmbed = new EmbedBuilder().WithTitle(string.Format(Messages.EventStarted, scheduledEvent.Name))
                .WithDescription(embedDescription)
                .WithColour(ColorsList.Green)
                .WithCurrentTimestamp()
                .Build();

            if (!startedEmbed.IsDefined(out var startedBuilt))
            {
                return Result.FromError(startedEmbed);
            }

            return (Result)await _channelApi.CreateMessageAsync(
                GuildSettings.EventNotificationChannel.Get(data.Settings),
                content, embeds: new[] { startedBuilt }, ct: ct);
        }

        if (scheduledEvent.Status != GuildScheduledEventStatus.Completed)
        {
            return new ArgumentOutOfRangeError(nameof(scheduledEvent.Status));
        }

        data.ScheduledEvents.Remove(scheduledEvent.ID.Value);

        var completedEmbed = new EmbedBuilder().WithTitle(string.Format(Messages.EventCompleted, scheduledEvent.Name))
            .WithDescription(
                string.Format(
                    Messages.EventDuration,
                    DateTimeOffset.UtcNow.Subtract(
                        data.ScheduledEvents[scheduledEvent.ID.Value].ActualStartTime
                        ?? scheduledEvent.ScheduledStartTime).ToString()))
            .WithColour(ColorsList.Black)
            .WithCurrentTimestamp()
            .Build();

        if (!completedEmbed.IsDefined(out var completedBuilt))
        {
            return Result.FromError(completedEmbed);
        }

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(data.Settings),
            embeds: new[] { completedBuilt }, ct: ct);
    }

    private static Result<string> GetLocalEventStartedEmbedDescription(IGuildScheduledEvent scheduledEvent)
    {
        if (scheduledEvent.ChannelID is null)
        {
            return new ArgumentNullError(nameof(scheduledEvent.ChannelID));
        }

        return string.Format(
            Messages.DescriptionLocalEventStarted,
            Mention.Channel(scheduledEvent.ChannelID.Value)
        );
    }

    private static Result<string> GetExternalEventStartedEmbedDescription(IGuildScheduledEvent scheduledEvent)
    {
        var dataResult = scheduledEvent.TryGetExternalEventData(out var endTime, out var location);
        if (!dataResult.IsSuccess)
        {
            return Result<string>.FromError(dataResult);
        }

        return string.Format(
            Messages.DescriptionExternalEventStarted,
            Markdown.InlineCode(location ?? string.Empty),
            Markdown.Timestamp(endTime)
        );
    }

    private async Task<Result> SendEarlyEventNotificationAsync(
        IGuildScheduledEvent scheduledEvent, GuildData data, CancellationToken ct)
    {
        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var contentResult = await _utility.GetEventNotificationMentions(
            scheduledEvent, data.Settings, ct);
        if (!contentResult.IsDefined(out var content))
        {
            return Result.FromError(contentResult);
        }

        var earlyResult = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventEarlyNotification, scheduledEvent.Name), currentUser)
            .WithColour(ColorsList.Default)
            .WithCurrentTimestamp()
            .Build();

        if (!earlyResult.IsDefined(out var earlyBuilt))
        {
            return Result.FromError(earlyResult);
        }

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(data.Settings),
            content,
            embeds: new[] { earlyBuilt }, ct: ct);
    }
}
