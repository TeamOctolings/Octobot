using System.Text.Json.Nodes;
using Boyfriend.Data;
using Boyfriend.locale;
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
    ///         <item>Automatically grants members the guild's <see cref="GuildSettings.DefaultRole"/> if one is set.</item>
    ///         <item>Sends reminders about an upcoming scheduled event.</item>
    ///         <item>Automatically starts scheduled events if <see cref="GuildSettings.AutoStartEvents"/> is enabled.</item>
    ///         <item>Sends scheduled event start notifications.</item>
    ///         <item>Sends scheduled event completion notifications.</item>
    ///         <item>Sends reminders to members.</item>
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
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        var defaultRole = GuildSettings.DefaultRole.Get(data.Settings);

        foreach (var memberData in data.MemberData.Values) {
            var userId = memberData.Id.ToSnowflake();

            if (defaultRole.Value is not 0 && !memberData.Roles.Contains(defaultRole.Value))
                _ = _guildApi.AddGuildMemberRoleAsync(
                    guildId, userId, defaultRole, ct: ct);

            if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
                var unbanResult = await _guildApi.RemoveGuildBanAsync(
                    guildId, userId, Messages.PunishmentExpired.EncodeHeader(), ct);
                if (unbanResult.IsSuccess)
                    memberData.BannedUntil = null;
                else
                    _logger.LogWarning(
                        "Error in automatic user unban request.\n{ErrorMessage}", unbanResult.Error.Message);
            }

            var userResult = await _userApi.GetUserAsync(userId, ct);
            if (!userResult.IsDefined(out var user)) continue;

            for (var i = memberData.Reminders.Count - 1; i >= 0; i--) {
                var reminder = memberData.Reminders[i];
                if (DateTimeOffset.UtcNow < reminder.At) continue;

                var embed = new EmbedBuilder().WithSmallTitle(
                        string.Format(Messages.Reminder, user.GetTag()), user)
                    .WithDescription(
                        string.Format(Messages.DescriptionReminder, Markdown.InlineCode(reminder.Text)))
                    .WithColour(ColorsList.Magenta)
                    .Build();

                if (!embed.IsDefined(out var built)) continue;

                var messageResult = await _channelApi.CreateMessageAsync(
                    reminder.Channel.ToSnowflake(), Mention.User(user), embeds: new[] { built }, ct: ct);
                if (!messageResult.IsSuccess)
                    _logger.LogWarning(
                        "Error in reminder send.\n{ErrorMessage}", messageResult.Error.Message);

                memberData.Reminders.Remove(reminder);
            }
        }

        var eventsResult = await _eventApi.ListScheduledEventsForGuildAsync(guildId, ct: ct);
        if (!eventsResult.IsDefined(out var events)) return;

        if (GuildSettings.EventNotificationChannel.Get(data.Settings).Empty()) return;

        foreach (var scheduledEvent in events) {
            if (!data.ScheduledEvents.ContainsKey(scheduledEvent.ID.Value)) {
                data.ScheduledEvents.Add(scheduledEvent.ID.Value, new ScheduledEventData(scheduledEvent.Status));
            } else {
                var storedEvent = data.ScheduledEvents[scheduledEvent.ID.Value];
                if (storedEvent.Status == scheduledEvent.Status) {
                    if (DateTimeOffset.UtcNow >= scheduledEvent.ScheduledStartTime) {
                        if (GuildSettings.AutoStartEvents.Get(data.Settings)
                            && scheduledEvent.Status is not GuildScheduledEventStatus.Active) {
                            var startResult = await _eventApi.ModifyGuildScheduledEventAsync(
                                guildId, scheduledEvent.ID,
                                status: GuildScheduledEventStatus.Active, ct: ct);
                            if (!startResult.IsSuccess)
                                _logger.LogWarning(
                                    "Error in automatic scheduled event start request.\n{ErrorMessage}",
                                    startResult.Error.Message);
                        }
                    } else if (GuildSettings.EventEarlyNotificationOffset.Get(data.Settings) != TimeSpan.Zero
                               && !storedEvent.EarlyNotificationSent
                               && DateTimeOffset.UtcNow
                               >= scheduledEvent.ScheduledStartTime
                               - GuildSettings.EventEarlyNotificationOffset.Get(data.Settings)) {
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
                    await SendScheduledEventCreatedMessage(scheduledEvent, data.Settings, ct),
                GuildScheduledEventStatus.Active or GuildScheduledEventStatus.Completed =>
                    await SendScheduledEventUpdatedMessage(scheduledEvent, data, false, ct),
                _ => Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.Status)))
            };

            if (!result.IsSuccess)
                _logger.LogWarning("Error in guild update.\n{ErrorMessage}", result.Error.Message);
        }
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
        IGuildScheduledEvent scheduledEvent, JsonNode settings, CancellationToken ct = default) {
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

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildSettings.EventStartedReceivers" />s,
    ///     when a scheduled event is about to start, has started or completed
    ///     in a guild's <see cref="GuildSettings.EventNotificationChannel" /> if one is set.
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
                        scheduledEvent, data.Settings, ct);
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
            GuildSettings.EventNotificationChannel.Get(data.Settings),
            content ?? default(Optional<string>), embeds: new[] { built }, ct: ct);
    }
}
