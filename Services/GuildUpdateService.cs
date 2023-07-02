using Boyfriend.Data;
using Boyfriend.Services.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Services;

/// <summary>
///     Handles executing guild updates (also called "ticks") once per second.
/// </summary>
public class GuildUpdateService : BackgroundService {
    private static readonly (string Name, TimeSpan Duration)[] SongList = {
        ("UNDEAD CORPORATION - The Empress", new TimeSpan(0, 4, 34)),
        ("UNDEAD CORPORATION - Everything will freeze", new TimeSpan(0, 3, 17)),
        ("Splatoon 3 - Rockagilly Blues (Yoko & the Gold Bazookas)", new TimeSpan(0, 3, 37)),
        ("Splatoon 3 - Seep and Destroy", new TimeSpan(0, 2, 42)),
        ("IA - A Tale of Six Trillion Years and a Night", new TimeSpan(0, 3, 40)),
        ("Manuel - Gas Gas Gas", new TimeSpan(0, 3, 17)),
        ("Camellia - Flamewall", new TimeSpan(0, 6, 50))
    };

    private readonly List<Activity> _activityList = new(1) { new Activity("with Remora.Discord", ActivityType.Game) };

    private readonly IDiscordRestChannelAPI             _channelApi;
    private readonly DiscordGatewayClient               _client;
    private readonly GuildDataService                   _dataService;
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;
    private readonly IDiscordRestGuildAPI               _guildApi;
    private readonly ILogger<GuildUpdateService>        _logger;
    private readonly IDiscordRestUserAPI                _userApi;
    private readonly UtilityService                     _utility;

    private DateTimeOffset _nextSongAt = DateTimeOffset.MinValue;
    private uint           _nextSongIndex;

    public GuildUpdateService(
        IDiscordRestChannelAPI             channelApi, DiscordGatewayClient client, GuildDataService dataService,
        IDiscordRestGuildScheduledEventAPI eventApi, IDiscordRestGuildAPI guildApi, ILogger<GuildUpdateService> logger,
        IDiscordRestUserAPI                userApi, UtilityService utility) {
        _channelApi = channelApi;
        _client = client;
        _dataService = dataService;
        _eventApi = eventApi;
        _guildApi = guildApi;
        _logger = logger;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     Activates a periodic timer with a 1 second interval and adds guild update tasks on each timer tick.
    ///     Additionally, updates the current presence with songs from <see cref="SongList"/>.
    /// </summary>
    /// <remarks>If update tasks take longer than 1 second, the next timer tick will be skipped.</remarks>
    /// <param name="ct">The cancellation token for this operation.</param>
    protected override async Task ExecuteAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var tasks = new List<Task>();

        while (await timer.WaitForNextTickAsync(ct)) {
            var guildIds = _dataService.GetGuildIds();
            if (guildIds.Count > 0 && DateTimeOffset.UtcNow >= _nextSongAt) {
                var nextSong = SongList[_nextSongIndex];
                _activityList[0] = new Activity(nextSong.Name, ActivityType.Listening);
                _client.SubmitCommand(
                    new UpdatePresence(
                        UserStatus.Online, false, DateTimeOffset.UtcNow, _activityList));
                _nextSongAt = DateTimeOffset.UtcNow.Add(nextSong.Duration);
                _nextSongIndex++;
                if (_nextSongIndex >= SongList.Length) _nextSongIndex = 0;
            }

            tasks.AddRange(guildIds.Select(id => TickGuildAsync(id, ct)));

            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    /// <summary>
    ///     Runs an update ("tick") for a guild with the provided <paramref name="guildId" />.
    /// </summary>
    /// <remarks>
    ///     This method does the following:
    ///     <list type="bullet">
    ///         <item>Automatically unbans users once their ban period has expired.</item>
    ///         <item>Automatically grants users the guild's <see cref="GuildConfiguration.DefaultRole"/> if one is set.</item>
    ///         <item>Sends reminders about an upcoming scheduled event.</item>
    ///         <item>Automatically starts scheduled events if <see cref="GuildConfiguration.AutoStartEvents"/> is enabled.</item>
    ///         <item>Sends scheduled event start notifications.</item>
    ///         <item>Sends scheduled event completion notifications.</item>
    ///     </list>
    ///     This is done here and not in a <see cref="IResponder{TGatewayEvent}" /> for the following reasons:
    ///     <list type="bullet">
    ///         <item>
    ///             Downtime would affect the reliability of notifications and automatic unbans if this logic were to be in a
    ///             <see cref="IResponder{TGatewayEvent}" />.
    ///         </item>
    ///         <item>The Discord API doesn't provide necessary information about scheduled event updates.</item>
    ///     </list>
    /// </remarks>
    /// <param name="guildId">The ID of the guild to update.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    private async Task TickGuildAsync(Snowflake guildId, CancellationToken ct = default) {
        var data = await _dataService.GetData(guildId, ct);
        Messages.Culture = data.Culture;
        var defaultRoleSnowflake = data.Configuration.DefaultRole.ToDiscordSnowflake();

        foreach (var memberData in data.MemberData.Values) {
            var userIdSnowflake = memberData.Id.ToDiscordSnowflake();
            if (defaultRoleSnowflake.Value is not 0 && !memberData.Roles.Contains(defaultRoleSnowflake))
                _ = _guildApi.AddGuildMemberRoleAsync(
                    guildId, userIdSnowflake, defaultRoleSnowflake, ct: ct);

            if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
                var unbanResult = await _guildApi.RemoveGuildBanAsync(
                    guildId, userIdSnowflake, Messages.PunishmentExpired.EncodeHeader(), ct);
                if (unbanResult.IsSuccess)
                    memberData.BannedUntil = null;
                else
                    _logger.LogWarning(
                        "Error in automatic user unban request.\n{ErrorMessage}", unbanResult.Error.Message);
            }
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
                    if (DateTimeOffset.UtcNow >= scheduledEvent.ScheduledStartTime) {
                        if (scheduledEvent.Status is not GuildScheduledEventStatus.Active) {
                            var startResult = await _eventApi.ModifyGuildScheduledEventAsync(
                                guildId, scheduledEvent.ID,
                                status: GuildScheduledEventStatus.Active, ct: ct);
                            if (!startResult.IsSuccess)
                                _logger.LogWarning(
                                    "Error in automatic scheduled event start request.\n{ErrorMessage}",
                                    startResult.Error.Message);
                        }
                    } else if (DateTimeOffset.UtcNow
                               >= scheduledEvent.ScheduledStartTime - data.Configuration.EventEarlyNotificationOffset
                               && !storedEvent.EarlyNotificationSent) {
                        var earlyResult = await SendScheduledEventUpdatedMessage(scheduledEvent, data, true, ct);
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
                    await SendScheduledEventUpdatedMessage(scheduledEvent, data, false, ct),
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
    /// <param name="scheduledEvent">The scheduled event that has just been created.</param>
    /// <param name="config">The configuration of the guild containing the scheduled event.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A notification sending result which may or may not have succeeded.</returns>
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
    /// <param name="scheduledEvent">The scheduled event that is about to start, has started or completed.</param>
    /// <param name="data">The data for the guild containing the scheduled event.</param>
    /// <param name="early">Controls whether or not a reminder for the scheduled event should be sent instead of the event started/completed notification</param>
    /// <param name="ct">The cancellation token for this operation</param>
    /// <returns>A reminder/notification sending result which may or may not have succeeded.</returns>
    private async Task<Result> SendScheduledEventUpdatedMessage(
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

                    var contentResult = await _utility.GetEventNotificationMentions(
                        scheduledEvent, data.Configuration, ct);
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
