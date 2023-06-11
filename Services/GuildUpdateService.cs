using Boyfriend.Data;
using Boyfriend.Services.Data;
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

namespace Boyfriend.Services;

public class GuildUpdateService : BackgroundService {
    private readonly IDiscordRestChannelAPI             _channelApi;
    private readonly GuildDataService                   _dataService;
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;
    private readonly IDiscordRestGuildAPI               _guildApi;
    private readonly ILogger<GuildUpdateService>        _logger;
    private readonly IDiscordRestUserAPI                _userApi;
    private readonly UtilityService                     _utility;

    public GuildUpdateService(
        IDiscordRestChannelAPI             channelApi, GuildDataService dataService, IDiscordRestGuildAPI guildApi,
        IDiscordRestGuildScheduledEventAPI eventApi,   ILogger<GuildUpdateService> logger, IDiscordRestUserAPI userApi,
        UtilityService                     utility) {
        _channelApi = channelApi;
        _dataService = dataService;
        _guildApi = guildApi;
        _eventApi = eventApi;
        _logger = logger;
        _userApi = userApi;
        _utility = utility;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var tasks = new List<Task>();

        while (await timer.WaitForNextTickAsync(ct)) {
            tasks.AddRange(_dataService.GetGuildIds().Select(id => TickGuildAsync(id, ct)));

            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    private async Task TickGuildAsync(Snowflake guildId, CancellationToken ct = default) {
        var data = await _dataService.GetData(guildId, ct);
        Messages.Culture = data.Culture;

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var memberData in data.MemberData.Values)
            if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
                var unbanResult = await _guildApi.RemoveGuildBanAsync(
                    guildId, memberData.Id.ToDiscordSnowflake(), Messages.PunishmentExpired.EncodeHeader(), ct);
                if (unbanResult.IsSuccess)
                    memberData.BannedUntil = null;
                else
                    _logger.LogWarning("Error in member data update.\n{ErrorMessage}", unbanResult.Error.Message);
            }

        var eventsResult = await _eventApi.ListScheduledEventsForGuildAsync(guildId, ct: ct);
        if (!eventsResult.IsDefined(out var events)) return;

        if (data.Configuration.EventNotificationChannel is 0) return;

        foreach (var scheduledEvent in events) {
            if (!data.ScheduledEvents.ContainsKey(scheduledEvent.ID.Value)) {
                data.ScheduledEvents.Add(scheduledEvent.ID.Value, new ScheduledEventData(scheduledEvent.Status));
            } else {
                var storedEvent = data.ScheduledEvents[scheduledEvent.ID.Value];
                if (storedEvent.Status == scheduledEvent.Status) {
                    if (DateTimeOffset.UtcNow
                        >= scheduledEvent.ScheduledStartTime - data.Configuration.EventEarlyNotificationOffset
                        && !storedEvent.EarlyNotificationSent) {
                        var earlyResult = await SendScheduledEventStartedMessage(scheduledEvent, data, true, ct);
                        if (earlyResult.IsSuccess)
                            storedEvent.EarlyNotificationSent = true;
                        else
                            _logger.LogWarning(
                                "Error in scheduled event early notification sender.\n{ErrorMessage}",
                                earlyResult.Error.Message);
                    }

                    continue;
                }

                storedEvent.Status = scheduledEvent.Status;
            }

            var result = scheduledEvent.Status switch {
                GuildScheduledEventStatus.Scheduled =>
                    await SendScheduledEventCreatedMessage(scheduledEvent, data.Configuration, ct),
                GuildScheduledEventStatus.Active or GuildScheduledEventStatus.Completed =>
                    await SendScheduledEventStartedMessage(scheduledEvent, data, false, ct),
                _ => Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.Status)))
            };

            if (!result.IsSuccess)
                _logger.LogWarning("Error in guild update.\n{ErrorMessage}", result.Error.Message);
        }
    }

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildConfiguration.EventNotificationRole" /> if one is
    ///     set,
    ///     when a scheduled event is created
    ///     in a guild's <see cref="GuildConfiguration.EventNotificationChannel" /> if one is set.
    /// </summary>
    private async Task<Result> SendScheduledEventCreatedMessage(
        IGuildScheduledEvent scheduledEvent, GuildConfiguration config, CancellationToken ct = default) {
        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        if (!scheduledEvent.CreatorID.IsDefined(out var creatorId))
            return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.CreatorID)));
        var creatorResult = await _userApi.GetUserAsync(creatorId.Value, ct);
        if (!creatorResult.IsDefined(out var creator)) return Result.FromError(creatorResult);

        string embedDescription;
        var eventDescription = scheduledEvent.Description is { HasValue: true, Value: not null }
            ? scheduledEvent.Description.Value
            : string.Empty;
        switch (scheduledEvent.EntityType) {
            case GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice:
                if (!scheduledEvent.ChannelID.AsOptional().IsDefined(out var channelId))
                    return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.ChannelID)));

                embedDescription = $"{eventDescription}\n\n{Markdown.BlockQuote(
                    string.Format(
                        Messages.DescriptionLocalEventCreated,
                        Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                        Mention.Channel(channelId)
                    ))}";
                break;
            case GuildScheduledEventEntityType.External:
                if (!scheduledEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
                    return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.EntityMetadata)));
                if (!scheduledEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
                    return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime)));
                if (!metadata.Location.IsDefined(out var location))
                    return Result.FromError(new ArgumentNullError(nameof(metadata.Location)));

                embedDescription = $"{eventDescription}\n\n{Markdown.BlockQuote(
                    string.Format(
                        Messages.DescriptionExternalEventCreated,
                        Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                        Markdown.Timestamp(endTime),
                        Markdown.InlineCode(location)
                    ))}";
                break;
            default:
                return Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType)));
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCreatedTitle, creator.GetTag()), creator)
            .WithTitle(scheduledEvent.Name)
            .WithDescription(embedDescription)
            .WithEventCover(scheduledEvent.ID, scheduledEvent.Image)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(ColorsList.White)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        var roleMention = config.EventNotificationRole is not 0
            ? Mention.Role(config.EventNotificationRole.ToDiscordSnowflake())
            : string.Empty;

        var button = new ButtonComponent(
            ButtonComponentStyle.Primary,
            Messages.EventDetailsButton,
            new PartialEmoji(Name: "📋"),
            CustomIDHelpers.CreateButtonIDWithState(
                "scheduled-event-details", $"{scheduledEvent.GuildID}:{scheduledEvent.ID}")
        );

        return (Result)await _channelApi.CreateMessageAsync(
            config.EventNotificationChannel.ToDiscordSnowflake(), roleMention, embeds: new[] { built },
            components: new[] { new ActionRowComponent(new[] { button }) }, ct: ct);
    }

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildConfiguration.EventStartedReceivers" />s,
    ///     when a scheduled event is about to start, has started or completed
    ///     in a guild's <see cref="GuildConfiguration.EventNotificationChannel" /> if one is set.
    /// </summary>
    private async Task<Result> SendScheduledEventStartedMessage(
        IGuildScheduledEvent scheduledEvent, GuildData data, bool early, CancellationToken ct = default) {
        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        var embed = new EmbedBuilder();
        string? content = null;
        if (early)
            embed.WithSmallTitle(string.Format(Messages.EventEarlyNotification, scheduledEvent.Name), currentUser)
                .WithColour(ColorsList.Default);
        else
            switch (scheduledEvent.Status) {
                case GuildScheduledEventStatus.Active:
                    data.ScheduledEvents[scheduledEvent.ID.Value].ActualStartTime = DateTimeOffset.UtcNow;

                    string embedDescription;
                    switch (scheduledEvent.EntityType) {
                        case GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice:
                            if (!scheduledEvent.ChannelID.AsOptional().IsDefined(out var channelId))
                                return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.ChannelID)));

                            embedDescription = string.Format(
                                Messages.DescriptionLocalEventStarted,
                                Mention.Channel(channelId)
                            );
                            break;
                        case GuildScheduledEventEntityType.External:
                            if (!scheduledEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
                                return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.EntityMetadata)));
                            if (!scheduledEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
                                return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime)));
                            if (!metadata.Location.IsDefined(out var location))
                                return Result.FromError(new ArgumentNullError(nameof(metadata.Location)));

                            embedDescription = string.Format(
                                Messages.DescriptionExternalEventStarted,
                                Markdown.InlineCode(location),
                                Markdown.Timestamp(endTime)
                            );
                            break;
                        default:
                            return Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType)));
                    }

                    var contentResult = await _utility.GetEventNotificationMentions(data, scheduledEvent, ct);
                    if (!contentResult.IsDefined(out content))
                        return Result.FromError(contentResult);

                    embed.WithTitle(string.Format(Messages.EventStarted, scheduledEvent.Name))
                        .WithDescription(embedDescription)
                        .WithColour(ColorsList.Green);
                    break;
                case GuildScheduledEventStatus.Completed:
                    embed.WithTitle(string.Format(Messages.EventCompleted, scheduledEvent.Name))
                        .WithDescription(
                            string.Format(
                                Messages.EventDuration,
                                DateTimeOffset.UtcNow.Subtract(
                                    data.ScheduledEvents[scheduledEvent.ID.Value].ActualStartTime
                                    ?? scheduledEvent.ScheduledStartTime).ToString()))
                        .WithColour(ColorsList.Black);

                    data.ScheduledEvents.Remove(scheduledEvent.ID.Value);
                    break;
                case GuildScheduledEventStatus.Canceled:
                case GuildScheduledEventStatus.Scheduled:
                default: return Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.Status)));
            }

        var result = embed.WithCurrentTimestamp().Build();

        if (!result.IsDefined(out var built)) return Result.FromError(result);

        return (Result)await _channelApi.CreateMessageAsync(
            data.Configuration.EventNotificationChannel.ToDiscordSnowflake(),
            content ?? default(Optional<string>), embeds: new[] { built }, ct: ct);
    }
}
