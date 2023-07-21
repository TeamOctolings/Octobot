using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boyfriend.Data;
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
public partial class GuildUpdateService : BackgroundService {
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
            var userResult = await _userApi.GetUserAsync(memberData.Id.ToSnowflake(), ct);
            if (!userResult.IsDefined(out var user)) return;

            var guildUser = await _guildApi.GetGuildMemberAsync(guildId, user.ID, ct);
            var random = new Random();

            var pattern = IllegalCharsRegex();
            var match = pattern.Match(guildUser.Entity.Nickname.ToString());
            var nickname = match.Groups[1].Value;
            if (match.Groups[1].Value is "") {
                var nicknames = new List<string> {
                    "Albatross", "Alpha", "Anchor", "Banjo", "Bell", "Beta", "Blackbird", "Bulldog", "Canary",
                    "Cat", "Calf", "Cyclone", "Daisy", "Dalmatian", "Dart", "Delta", "Diamond", "Donkey", "Duck",
                    "Emu", "Eclipse", "Flamingo", "Flute", "Frog", "Goose", "Hatchet", "Heron", "Husky", "Hurricane",
                    "Iceberg", "Iguana", "Kiwi", "Kite", "Lamb", "Lily", "Macaw", "Manatee", "Maple", "Mask",
                    "Nautilus", "Ostrich", "Octopus", "Pelican", "Puffin", "Pyramid", "Rattle", "Robin", "Rose",
                    "Salmon", "Seal", "Shark", "Sheep", "Snake", "Sonar", "Stump", "Sparrow", "Toaster", "Toucan",
                    "Torus", "Violet", "Vortex", "Vulture", "Wagon", "Whale", "Woodpecker", "Zebra", "Zigzag"
                };
                nickname = nicknames[random.Next(nicknames.Count)];
            }
            await _guildApi.ModifyGuildMemberAsync(guildId, user.ID, nickname, ct: ct);
            await TickMemberAsync(guildId, user, memberData, defaultRole, ct);
        }

        var eventsResult = await _eventApi.ListScheduledEventsForGuildAsync(guildId, ct: ct);
        if (!eventsResult.IsSuccess)
            _logger.LogWarning("Error retrieving scheduled events.\n{ErrorMessage}", eventsResult.Error.Message);
        else if (!GuildSettings.EventNotificationChannel.Get(data.Settings).Empty())
            await TickScheduledEventsAsync(guildId, data, eventsResult.Entity, ct);
    }

    [GeneratedRegex("^[~`!?@#№$%^&*:;.,()<>{}\\[\\]\\-_=+/\\\\|']*(.*)")]
    private static partial Regex IllegalCharsRegex();

    private async Task TickScheduledEventsAsync(
        Snowflake guildId, GuildData data, IEnumerable<IGuildScheduledEvent> events, CancellationToken ct) {
        foreach (var scheduledEvent in events) {
            if (!data.ScheduledEvents.ContainsKey(scheduledEvent.ID.Value))
                data.ScheduledEvents.Add(scheduledEvent.ID.Value, new ScheduledEventData(scheduledEvent.Status));

            var storedEvent = data.ScheduledEvents[scheduledEvent.ID.Value];
            if (storedEvent.Status == scheduledEvent.Status) {
                await TickScheduledEventAsync(guildId, data, scheduledEvent, storedEvent, ct);
                continue;
            }

            storedEvent.Status = scheduledEvent.Status;

            var statusChangedResponseResult = storedEvent.Status switch {
                GuildScheduledEventStatus.Scheduled =>
                    await SendScheduledEventCreatedMessage(scheduledEvent, data.Settings, ct),
                GuildScheduledEventStatus.Active or GuildScheduledEventStatus.Completed =>
                    await SendScheduledEventUpdatedMessage(scheduledEvent, data, ct),
                _ => Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.Status)))
            };

            if (!statusChangedResponseResult.IsSuccess)
                _logger.LogWarning(
                    "Error handling scheduled event status update.\n{ErrorMessage}",
                    statusChangedResponseResult.Error.Message);
        }
    }

    private async Task TickScheduledEventAsync(
        Snowflake         guildId, GuildData data, IGuildScheduledEvent scheduledEvent, ScheduledEventData eventData,
        CancellationToken ct) {
        if (DateTimeOffset.UtcNow >= scheduledEvent.ScheduledStartTime) {
            await TryAutoStartEventAsync(guildId, data, scheduledEvent, ct);
            return;
        }

        if (GuildSettings.EventEarlyNotificationOffset.Get(data.Settings) == TimeSpan.Zero
            || eventData.EarlyNotificationSent
            || DateTimeOffset.UtcNow
            < scheduledEvent.ScheduledStartTime
            - GuildSettings.EventEarlyNotificationOffset.Get(data.Settings)) return;

        var earlyResult = await SendEarlyEventNotificationAsync(scheduledEvent, data, ct);
        if (earlyResult.IsSuccess) {
            eventData.EarlyNotificationSent = true;
            return;
        }

        _logger.LogWarning(
            "Error in scheduled event early notification sender.\n{ErrorMessage}",
            earlyResult.Error.Message);
    }

    private async Task TryAutoStartEventAsync(
        Snowflake guildId, GuildData data, IGuildScheduledEvent scheduledEvent, CancellationToken ct) {
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
    }

    private async Task TickMemberAsync(
        Snowflake guildId, IUser user, MemberData memberData, Snowflake defaultRole, CancellationToken ct) {
        if (defaultRole.Value is not 0 && !memberData.Roles.Contains(defaultRole.Value))
            _ = _guildApi.AddGuildMemberRoleAsync(
                guildId, user.ID, defaultRole, ct: ct);

        if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
            var unbanResult = await _guildApi.RemoveGuildBanAsync(
                guildId, user.ID, Messages.PunishmentExpired.EncodeHeader(), ct);
            if (unbanResult.IsSuccess)
                memberData.BannedUntil = null;
            else
                _logger.LogWarning(
                    "Error in automatic user unban request.\n{ErrorMessage}", unbanResult.Error.Message);
        }

        for (var i = memberData.Reminders.Count - 1; i >= 0; i--)
            await TickReminderAsync(memberData.Reminders[i], user, memberData, ct);
    }

    private async Task TickReminderAsync(Reminder reminder, IUser user, MemberData memberData, CancellationToken ct) {
        if (DateTimeOffset.UtcNow < reminder.At) return;

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.Reminder, user.GetTag()), user)
            .WithDescription(
                string.Format(Messages.DescriptionReminder, Markdown.InlineCode(reminder.Text)))
            .WithColour(ColorsList.Magenta)
            .Build();

        if (!embed.IsDefined(out var built)) return;

        var messageResult = await _channelApi.CreateMessageAsync(
            reminder.Channel.ToSnowflake(), Mention.User(user), embeds: new[] { built }, ct: ct);
        if (!messageResult.IsSuccess)
            _logger.LogWarning(
                "Error in reminder send.\n{ErrorMessage}", messageResult.Error.Message);

        memberData.Reminders.Remove(reminder);
    }

    private async Task TickScheduledEventAsync(
        Snowflake         guildId, GuildData data, IGuildScheduledEvent scheduledEvent, ScheduledEventData eventData,
        CancellationToken ct) {
        if (DateTimeOffset.UtcNow >= scheduledEvent.ScheduledStartTime) {
            await TryAutoStartEventAsync(guildId, data, scheduledEvent, ct);
            return;
        }

        if (GuildSettings.EventEarlyNotificationOffset.Get(data.Settings) == TimeSpan.Zero
            || eventData.EarlyNotificationSent
            || DateTimeOffset.UtcNow
            < scheduledEvent.ScheduledStartTime
            - GuildSettings.EventEarlyNotificationOffset.Get(data.Settings)) return;

        var earlyResult = await SendEarlyEventNotificationAsync(scheduledEvent, data, ct);
        if (earlyResult.IsSuccess) {
            eventData.EarlyNotificationSent = true;
            return;
        }

        _logger.LogWarning(
            "Error in scheduled event early notification sender.\n{ErrorMessage}",
            earlyResult.Error.Message);
    }

    private async Task TryAutoStartEventAsync(
        Snowflake guildId, GuildData data, IGuildScheduledEvent scheduledEvent, CancellationToken ct) {
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
    }

    private async Task TickMemberAsync(
        Snowflake guildId, IUser user, MemberData memberData, Snowflake defaultRole, CancellationToken ct) {
        if (defaultRole.Value is not 0 && !memberData.Roles.Contains(defaultRole.Value))
            _ = _guildApi.AddGuildMemberRoleAsync(
                guildId, user.ID, defaultRole, ct: ct);

        if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
            var unbanResult = await _guildApi.RemoveGuildBanAsync(
                guildId, user.ID, Messages.PunishmentExpired.EncodeHeader(), ct);
            if (unbanResult.IsSuccess)
                memberData.BannedUntil = null;
            else
                _logger.LogWarning(
                    "Error in automatic user unban request.\n{ErrorMessage}", unbanResult.Error.Message);
        }

        for (var i = memberData.Reminders.Count - 1; i >= 0; i--)
            await TickReminderAsync(memberData.Reminders[i], user, memberData, ct);
    }

    private async Task TickReminderAsync(Reminder reminder, IUser user, MemberData memberData, CancellationToken ct) {
        if (DateTimeOffset.UtcNow < reminder.At) return;

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.Reminder, user.GetTag()), user)
            .WithDescription(
                string.Format(Messages.DescriptionReminder, Markdown.InlineCode(reminder.Text)))
            .WithColour(ColorsList.Magenta)
            .Build();

        if (!embed.IsDefined(out var built)) return;

        var messageResult = await _channelApi.CreateMessageAsync(
            reminder.Channel.ToSnowflake(), Mention.User(user), embeds: new[] { built }, ct: ct);
        if (!messageResult.IsSuccess)
            _logger.LogWarning(
                "Error in reminder send.\n{ErrorMessage}", messageResult.Error.Message);

        memberData.Reminders.Remove(reminder);
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
        if (!scheduledEvent.Creator.IsDefined(out var creator))
            return Result.FromError(new ArgumentNullError(nameof(scheduledEvent.Creator)));

        Result<string> embedDescriptionResult;
        var eventDescription = scheduledEvent.Description is { HasValue: true, Value: not null }
            ? scheduledEvent.Description.Value
            : string.Empty;
        embedDescriptionResult = scheduledEvent.EntityType switch {
            GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice =>
                GetLocalEventCreatedEmbedDescription(scheduledEvent, eventDescription),
            GuildScheduledEventEntityType.External => GetExternalScheduledEventCreatedEmbedDescription(
                scheduledEvent, eventDescription),
            _ => Result<string>.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType)))
        };

        if (!embedDescriptionResult.IsDefined(out var embedDescription))
            return Result.FromError(embedDescriptionResult);

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCreatedTitle, creator.GetTag()), creator)
            .WithTitle(scheduledEvent.Name)
            .WithDescription(embedDescription)
            .WithEventCover(scheduledEvent.ID, scheduledEvent.Image)
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

    private static Result<string> GetExternalScheduledEventCreatedEmbedDescription(
        IGuildScheduledEvent scheduledEvent, string eventDescription) {
        Result<string> embedDescription;
        if (!scheduledEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.EntityMetadata)));
        if (!scheduledEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime)));
        if (!metadata.Location.IsDefined(out var location))
            return Result<string>.FromError(new ArgumentNullError(nameof(metadata.Location)));

        embedDescription = $"{eventDescription}\n\n{Markdown.BlockQuote(
            string.Format(
                Messages.DescriptionExternalEventCreated,
                Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                Markdown.Timestamp(endTime),
                Markdown.InlineCode(location)
            ))}";
        return embedDescription;
    }

    private static Result<string> GetLocalEventCreatedEmbedDescription(
        IGuildScheduledEvent scheduledEvent, string eventDescription) {
        if (!scheduledEvent.ChannelID.AsOptional().IsDefined(out var channelId))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.ChannelID)));

        return $"{eventDescription}\n\n{Markdown.BlockQuote(
            string.Format(
                Messages.DescriptionLocalEventCreated,
                Markdown.Timestamp(scheduledEvent.ScheduledStartTime),
                Mention.Channel(channelId)
            ))}";
    }

    /// <summary>
    ///     Handles sending a notification, mentioning the <see cref="GuildSettings.EventNotificationRole"/> and event subscribers,
    ///     when a scheduled event has started or completed
    ///     in a guild's <see cref="GuildSettings.EventNotificationChannel" /> if one is set.
    /// </summary>
    /// <param name="scheduledEvent">The scheduled event that is about to start, has started or completed.</param>
    /// <param name="data">The data for the guild containing the scheduled event.</param>
    /// <param name="ct">The cancellation token for this operation</param>
    /// <returns>A reminder/notification sending result which may or may not have succeeded.</returns>
    private async Task<Result> SendScheduledEventUpdatedMessage(
        IGuildScheduledEvent scheduledEvent, GuildData data, CancellationToken ct = default) {
        if (scheduledEvent.Status == GuildScheduledEventStatus.Active) {
            data.ScheduledEvents[scheduledEvent.ID.Value].ActualStartTime = DateTimeOffset.UtcNow;

            var embedDescriptionResult = scheduledEvent.EntityType switch {
                GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice =>
                    GetLocalEventStartedEmbedDescription(scheduledEvent),
                GuildScheduledEventEntityType.External => GetExternalEventStartedEmbedDescription(scheduledEvent),
                _ => Result<string>.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.EntityType)))
            };

            var contentResult = await _utility.GetEventNotificationMentions(
                scheduledEvent, data.Settings, ct);
            if (!contentResult.IsDefined(out var content))
                return Result.FromError(contentResult);
            if (!embedDescriptionResult.IsDefined(out var embedDescription))
                return Result.FromError(embedDescriptionResult);

            var startedEmbed = new EmbedBuilder().WithTitle(string.Format(Messages.EventStarted, scheduledEvent.Name))
                .WithDescription(embedDescription)
                .WithColour(ColorsList.Green)
                .WithCurrentTimestamp()
                .Build();

            if (!startedEmbed.IsDefined(out var startedBuilt)) return Result.FromError(startedEmbed);

            return (Result)await _channelApi.CreateMessageAsync(
                GuildSettings.EventNotificationChannel.Get(data.Settings),
                content, embeds: new[] { startedBuilt }, ct: ct);
        }

        if (scheduledEvent.Status != GuildScheduledEventStatus.Completed)
            return Result.FromError(new ArgumentOutOfRangeError(nameof(scheduledEvent.Status)));
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
            return Result.FromError(completedEmbed);

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(data.Settings),
            embeds: new[] { completedBuilt }, ct: ct);
    }

    private static Result<string> GetLocalEventStartedEmbedDescription(IGuildScheduledEvent scheduledEvent) {
        Result<string> embedDescription;
        if (!scheduledEvent.ChannelID.AsOptional().IsDefined(out var channelId))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.ChannelID)));

        embedDescription = string.Format(
            Messages.DescriptionLocalEventStarted,
            Mention.Channel(channelId)
        );
        return embedDescription;
    }

    private static Result<string> GetExternalEventStartedEmbedDescription(IGuildScheduledEvent scheduledEvent) {
        Result<string> embedDescription;
        if (!scheduledEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.EntityMetadata)));
        if (!scheduledEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
            return Result<string>.FromError(new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime)));
        if (!metadata.Location.IsDefined(out var location))
            return Result<string>.FromError(new ArgumentNullError(nameof(metadata.Location)));

        embedDescription = string.Format(
            Messages.DescriptionExternalEventStarted,
            Markdown.InlineCode(location),
            Markdown.Timestamp(endTime)
        );
        return embedDescription;
    }

    private async Task<Result> SendEarlyEventNotificationAsync(
        IGuildScheduledEvent scheduledEvent, GuildData data, CancellationToken ct) {
        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        var contentResult = await _utility.GetEventNotificationMentions(
            scheduledEvent, data.Settings, ct);
        if (!contentResult.IsDefined(out var content))
            return Result.FromError(contentResult);

        var earlyResult = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventEarlyNotification, scheduledEvent.Name), currentUser)
            .WithColour(ColorsList.Default)
            .WithCurrentTimestamp()
            .Build();

        if (!earlyResult.IsDefined(out var earlyBuilt)) return Result.FromError(earlyResult);

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(data.Settings),
            content,
            embeds: new[] { earlyBuilt }, ct: ct);
    }
}
