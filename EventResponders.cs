using System.Drawing;
using System.Text;
using Boyfriend.Data;
using Boyfriend.Services.Data;
using DiffPlex;
using DiffPlex.DiffBuilder;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Caching;
using Remora.Discord.Caching.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

// ReSharper disable UnusedType.Global

namespace Boyfriend;

public class GuildCreateResponder : IResponder<IGuildCreate> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly ILogger<GuildCreateResponder> _logger;
    private readonly IDiscordRestUserAPI _userApi;

    public GuildCreateResponder(
        IDiscordRestChannelAPI        channelApi, GuildDataService dataService, IDiscordRestUserAPI userApi,
        ILogger<GuildCreateResponder> logger) {
        _channelApi = channelApi;
        _dataService = dataService;
        _userApi = userApi;
        _logger = logger;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.Guild.IsT0) return Result.FromSuccess(); // is IAvailableGuild

        var guild = gatewayEvent.Guild.AsT0;
        _logger.LogInformation("Joined guild \"{Name}\"", guild.Name);

        var guildConfig = await _dataService.GetConfiguration(guild.ID, ct);
        if (!guildConfig.ReceiveStartupMessages)
            return Result.FromSuccess();
        if (guildConfig.PrivateFeedbackChannel is 0)
            return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        Messages.Culture = guildConfig.Culture;
        var i = Random.Shared.Next(1, 4);

        var embed = new EmbedBuilder()
            .WithTitle($"Beep{i}".Localized())
            .WithDescription(Messages.Ready)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(Color.Aqua)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfig.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built }, ct: ct);
    }
}

public class MessageDeletedResponder : IResponder<IMessageDelete> {
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly IDiscordRestUserAPI _userApi;

    public MessageDeletedResponder(
        IDiscordRestAuditLogAPI auditLogApi, IDiscordRestChannelAPI channelApi,
        GuildDataService        dataService, IDiscordRestUserAPI    userApi) {
        _auditLogApi = auditLogApi;
        _channelApi = channelApi;
        _dataService = dataService;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IMessageDelete gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId)) return Result.FromSuccess();

        var guildConfiguration = await _dataService.GetConfiguration(guildId, ct);
        if (guildConfiguration.PrivateFeedbackChannel is 0) return Result.FromSuccess();

        var messageResult = await _channelApi.GetChannelMessageAsync(gatewayEvent.ChannelID, gatewayEvent.ID, ct);
        if (!messageResult.IsDefined(out var message)) return Result.FromError(messageResult);
        if (string.IsNullOrWhiteSpace(message.Content)) return Result.FromSuccess();

        var auditLogResult = await _auditLogApi.GetGuildAuditLogAsync(
            guildId, actionType: AuditLogEvent.MessageDelete, limit: 1, ct: ct);
        if (!auditLogResult.IsDefined(out var auditLogPage)) return Result.FromError(auditLogResult);

        var auditLog = auditLogPage.AuditLogEntries.Single();
        if (!auditLog.Options.IsDefined(out var options))
            return Result.FromError(new ArgumentNullError(nameof(auditLog.Options)));

        var user = message.Author;
        if (options.ChannelID == gatewayEvent.ChannelID
            && DateTimeOffset.UtcNow.Subtract(auditLog.ID.Timestamp).TotalSeconds <= 2) {
            var userResult = await _userApi.GetUserAsync(auditLog.UserID!.Value, ct);
            if (!userResult.IsDefined(out user)) return Result.FromError(userResult);
        }

        Messages.Culture = guildConfiguration.Culture;

        var embed = new EmbedBuilder()
            .WithSmallTitle(
                string.Format(
                    Messages.CachedMessageDeleted,
                    message.Author.GetTag()), message.Author)
            .WithDescription(
                $"{Mention.Channel(gatewayEvent.ChannelID)}\n{Markdown.BlockCode(message.Content.SanitizeForBlockCode())}")
            .WithActionFooter(user)
            .WithTimestamp(message.Timestamp)
            .WithColour(Color.Firebrick)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

public class MessageEditedResponder : IResponder<IMessageUpdate> {
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly IDiscordRestUserAPI _userApi;

    public MessageEditedResponder(
        CacheService        cacheService, IDiscordRestChannelAPI channelApi, GuildDataService dataService,
        IDiscordRestUserAPI userApi) {
        _cacheService = cacheService;
        _channelApi = channelApi;
        _dataService = dataService;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IMessageUpdate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId))
            return Result.FromSuccess();
        var guildConfiguration = await _dataService.GetConfiguration(guildId, ct);
        if (guildConfiguration.PrivateFeedbackChannel is 0)
            return Result.FromSuccess();
        if (!gatewayEvent.Content.IsDefined(out var newContent))
            return Result.FromSuccess();
        if (!gatewayEvent.EditedTimestamp.IsDefined(out var timestamp))
            return Result.FromSuccess();

        if (!gatewayEvent.ChannelID.IsDefined(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ChannelID)));
        if (!gatewayEvent.ID.IsDefined(out var messageId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ID)));

        var cacheKey = new KeyHelpers.MessageCacheKey(channelId, messageId);
        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            cacheKey, ct);
        if (!messageResult.IsDefined(out var message)) return Result.FromError(messageResult);
        if (message.Content == newContent) return Result.FromSuccess();

        // Custom event responders are called earlier than responders responsible for message caching
        // This means that subsequent edit logs may contain the wrong content
        // We can work around this by evicting the message from the cache
        await _cacheService.EvictAsync<IMessage>(cacheKey, ct);
        // However, since we evicted the message, subsequent edits won't have a cached instance to work with
        // Getting the message will put it back in the cache, resolving all issues
        // We don't need to await this since the result is not needed
        // NOTE: Because this is not awaited, there may be a race condition depending on how fast clients are able to edit their messages
        // NOTE: Awaiting this might not even solve this if the same responder is called asynchronously
        _ = _channelApi.GetChannelMessageAsync(channelId, messageId, ct);

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        var diff = new SideBySideDiffBuilder(Differ.Instance).BuildDiffModel(message.Content, newContent, true, true);

        Messages.Culture = guildConfiguration.Culture;

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.CachedMessageEdited, message.Author.GetTag()), message.Author)
            .WithDescription($"https://discord.com/channels/{guildId}/{channelId}/{messageId}\n{diff.AsMarkdown()}")
            .WithUserFooter(currentUser)
            .WithTimestamp(timestamp.Value)
            .WithColour(Color.Gold)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

public class GuildMemberAddResponder : IResponder<IGuildMemberAdd> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly IDiscordRestGuildAPI _guildApi;

    public GuildMemberAddResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService dataService, IDiscordRestGuildAPI guildApi) {
        _channelApi = channelApi;
        _dataService = dataService;
        _guildApi = guildApi;
    }

    public async Task<Result> RespondAsync(IGuildMemberAdd gatewayEvent, CancellationToken ct = default) {
        var guildConfiguration = await _dataService.GetConfiguration(gatewayEvent.GuildID, ct);
        if (guildConfiguration.PublicFeedbackChannel is 0)
            return Result.FromSuccess();
        if (guildConfiguration.WelcomeMessage is "off" or "disable" or "disabled")
            return Result.FromSuccess();

        Messages.Culture = guildConfiguration.Culture;
        var welcomeMessage = guildConfiguration.WelcomeMessage is "default" or "reset"
            ? Messages.DefaultWelcomeMessage
            : guildConfiguration.WelcomeMessage;

        if (!gatewayEvent.User.IsDefined(out var user))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.User)));

        var guildResult = await _guildApi.GetGuildAsync(gatewayEvent.GuildID, ct: ct);
        if (!guildResult.IsDefined(out var guild)) return Result.FromError(guildResult);

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(welcomeMessage, user.GetTag(), guild.Name), user)
            .WithGuildFooter(guild)
            .WithTimestamp(gatewayEvent.JoinedAt)
            .WithColour(Color.LawnGreen)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

public class GuildScheduledEventCreateResponder : IResponder<IGuildScheduledEventCreate> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly IDiscordRestUserAPI _userApi;

    public GuildScheduledEventCreateResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService dataService,
        IDiscordRestUserAPI    userApi) {
        _channelApi = channelApi;
        _dataService = dataService;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventCreate gatewayEvent, CancellationToken ct = default) {
        var guildData = await _dataService.GetData(gatewayEvent.GuildID, ct);
        guildData.ScheduledEvents.Add(
            gatewayEvent.ID.Value, new ScheduledEventData(GuildScheduledEventStatus.Scheduled));

        if (guildData.Configuration.EventNotificationChannel is 0)
            return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        if (!gatewayEvent.CreatorID.IsDefined(out var creatorId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.CreatorID)));
        var creatorResult = await _userApi.GetUserAsync(creatorId.Value, ct);
        if (!creatorResult.IsDefined(out var creator)) return Result.FromError(creatorResult);

        Messages.Culture = guildData.Culture;

        string embedDescription;
        var eventDescription = gatewayEvent.Description is { HasValue: true, Value: not null }
            ? gatewayEvent.Description.Value
            : string.Empty;
        switch (gatewayEvent.EntityType) {
            case GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice:
                if (!gatewayEvent.ChannelID.AsOptional().IsDefined(out var channelId))
                    return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ChannelID)));

                embedDescription = $"{eventDescription}\n\n{Markdown.BlockQuote(
                    string.Format(
                        Messages.DescriptionLocalEventCreated,
                        Markdown.Timestamp(gatewayEvent.ScheduledStartTime),
                        Mention.Channel(channelId)
                    ))}";
                break;
            case GuildScheduledEventEntityType.External:
                if (!gatewayEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
                    return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.EntityMetadata)));
                if (!gatewayEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
                    return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ScheduledEndTime)));
                if (!metadata.Location.IsDefined(out var location))
                    return Result.FromError(new ArgumentNullError(nameof(metadata.Location)));

                embedDescription = $"{eventDescription}\n\n{Markdown.BlockQuote(
                    string.Format(
                        Messages.DescriptionExternalEventCreated,
                        Markdown.Timestamp(gatewayEvent.ScheduledStartTime),
                        Markdown.Timestamp(endTime),
                        Markdown.InlineCode(location)
                    ))}";
                break;
            default:
                return Result.FromError(new ArgumentOutOfRangeError(nameof(gatewayEvent.EntityType)));
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCreatedTitle, creator.GetTag()), creator)
            .WithTitle(gatewayEvent.Name)
            .WithDescription(embedDescription)
            .WithEventCover(gatewayEvent.ID, gatewayEvent.Image)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(Color.Gray)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        var roleMention = guildData.Configuration.EventNotificationRole is not 0
            ? Mention.Role(guildData.Configuration.EventNotificationRole.ToDiscordSnowflake())
            : string.Empty;

        var button = new ButtonComponent(
            ButtonComponentStyle.Primary,
            Messages.EventDetailsButton,
            new PartialEmoji(Name: "📋"),
            CustomIDHelpers.CreateButtonIDWithState(
                "scheduled-event-details", $"{gatewayEvent.GuildID}:{gatewayEvent.ID}")
        );

        return (Result)await _channelApi.CreateMessageAsync(
            guildData.Configuration.EventNotificationChannel.ToDiscordSnowflake(), roleMention, embeds: new[] { built },
            components: new[] { new ActionRowComponent(new[] { button }) }, ct: ct);
    }
}

public class GuildScheduledEventUpdateResponder : IResponder<IGuildScheduledEventUpdate> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;

    public GuildScheduledEventUpdateResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService dataService, IDiscordRestGuildScheduledEventAPI eventApi) {
        _channelApi = channelApi;
        _dataService = dataService;
        _eventApi = eventApi;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventUpdate gatewayEvent, CancellationToken ct = default) {
        var guildData = await _dataService.GetData(gatewayEvent.GuildID, ct);
        if (gatewayEvent.Status == guildData.ScheduledEvents[gatewayEvent.ID.Value].Status
            || guildData.Configuration.EventNotificationChannel is 0) return Result.FromSuccess();

        guildData.ScheduledEvents[gatewayEvent.ID.Value].Status = gatewayEvent.Status;

        var embed = new EmbedBuilder();
        StringBuilder? content = null;
        switch (gatewayEvent.Status) {
            case GuildScheduledEventStatus.Active:
                guildData.ScheduledEvents[gatewayEvent.ID.Value].ActualStartTime = DateTimeOffset.UtcNow;

                string embedDescription;
                switch (gatewayEvent.EntityType) {
                    case GuildScheduledEventEntityType.StageInstance or GuildScheduledEventEntityType.Voice:
                        if (!gatewayEvent.ChannelID.AsOptional().IsDefined(out var channelId))
                            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ChannelID)));

                        embedDescription = string.Format(
                            Messages.DescriptionLocalEventStarted,
                            Mention.Channel(channelId)
                        );
                        break;
                    case GuildScheduledEventEntityType.External:
                        if (!gatewayEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
                            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.EntityMetadata)));
                        if (!gatewayEvent.ScheduledEndTime.AsOptional().IsDefined(out var endTime))
                            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ScheduledEndTime)));
                        if (!metadata.Location.IsDefined(out var location))
                            return Result.FromError(new ArgumentNullError(nameof(metadata.Location)));

                        embedDescription = string.Format(
                            Messages.DescriptionExternalEventStarted,
                            Markdown.InlineCode(location),
                            Markdown.Timestamp(endTime)
                        );
                        break;
                    default:
                        return Result.FromError(new ArgumentOutOfRangeError(nameof(gatewayEvent.EntityType)));
                }

                content = new StringBuilder();
                var receivers = guildData.Configuration.EventStartedReceivers;
                var role = guildData.Configuration.EventNotificationRole.ToDiscordSnowflake();
                var usersResult = await _eventApi.GetGuildScheduledEventUsersAsync(
                    gatewayEvent.GuildID, gatewayEvent.ID, withMember: true, ct: ct);
                if (!usersResult.IsDefined(out var users)) return Result.FromError(usersResult);

                if (receivers.Contains(GuildConfiguration.NotificationReceiver.Role) && role.Value is not 0)
                    content.Append($"{Mention.Role(role)} ");
                if (receivers.Contains(GuildConfiguration.NotificationReceiver.Interested))
                    content = users.Where(
                            user => {
                                if (!user.GuildMember.IsDefined(out var member)) return true;
                                return !member.Roles.Contains(role);
                            })
                        .Aggregate(content, (current, user) => current.Append($"{Mention.User(user.User)} "));

                embed.WithTitle(string.Format(Messages.EventStarted, gatewayEvent.Name))
                    .WithDescription(embedDescription)
                    .WithCurrentTimestamp()
                    .WithColour(Color.LawnGreen);
                break;
            case GuildScheduledEventStatus.Completed:
                embed.WithTitle(string.Format(Messages.EventCompleted, gatewayEvent.Name))
                    .WithDescription(
                        string.Format(
                            Messages.EventDuration,
                            DateTimeOffset.UtcNow.Subtract(
                                guildData.ScheduledEvents[gatewayEvent.ID.Value].ActualStartTime
                                ?? gatewayEvent.ScheduledStartTime).ToString()))
                    .WithColour(Color.Black);

                guildData.ScheduledEvents.Remove(gatewayEvent.ID.Value);
                break;
            case GuildScheduledEventStatus.Canceled:
            case GuildScheduledEventStatus.Scheduled:
            default: return Result.FromError(new ArgumentOutOfRangeError(nameof(gatewayEvent.Status)));
        }

        var result = embed.WithCurrentTimestamp().Build();

        if (!result.IsDefined(out var built)) return Result.FromError(result);

        return (Result)await _channelApi.CreateMessageAsync(
            guildData.Configuration.EventNotificationChannel.ToDiscordSnowflake(),
            content?.ToString() ?? default(Optional<string>), embeds: new[] { built }, ct: ct);
    }
}

public class GuildScheduledEventResponder : IResponder<IGuildScheduledEventDelete> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _dataService;

    public GuildScheduledEventResponder(IDiscordRestChannelAPI channelApi, GuildDataService dataService) {
        _channelApi = channelApi;
        _dataService = dataService;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventDelete gatewayEvent, CancellationToken ct = default) {
        var guildData = await _dataService.GetData(gatewayEvent.GuildID, ct);
        guildData.ScheduledEvents.Remove(gatewayEvent.ID.Value);

        if (guildData.Configuration.EventNotificationChannel is 0)
            return Result.FromSuccess();

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCancelled, gatewayEvent.Name))
            .WithDescription(":(")
            .WithColour(Color.Firebrick)
            .WithCurrentTimestamp()
            .Build();

        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildData.Configuration.EventNotificationChannel.ToDiscordSnowflake(), embeds: new[] { built }, ct: ct);
    }
}
