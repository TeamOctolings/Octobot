using Boyfriend.Data;
using Boyfriend.Services.Data;
using DiffPlex;
using DiffPlex.DiffBuilder;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching;
using Remora.Discord.Caching.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

// ReSharper disable UnusedType.Global

namespace Boyfriend;

/// <summary>
///     Handles sending a <see cref="Messages.Ready" /> message to a guild that has just initialized if that guild
///     has <see cref="GuildConfiguration.ReceiveStartupMessages" /> enabled
/// </summary>
public class GuildCreateResponder : IResponder<IGuildCreate> {
    private readonly IDiscordRestChannelAPI        _channelApi;
    private readonly GuildDataService              _dataService;
    private readonly ILogger<GuildCreateResponder> _logger;
    private readonly IDiscordRestUserAPI           _userApi;

    public GuildCreateResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService dataService, ILogger<GuildCreateResponder> logger,
        IDiscordRestUserAPI    userApi) {
        _channelApi = channelApi;
        _dataService = dataService;
        _logger = logger;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.Guild.IsT0) return Result.FromSuccess(); // Guild isn't IAvailableGuild

        var guild = gatewayEvent.Guild.AsT0;
        _logger.LogInformation("Joined guild \"{Name}\"", guild.Name);

        var guildConfig = await _dataService.GetConfiguration(guild.ID, ct);
        if (!guildConfig.ReceiveStartupMessages)
            return Result.FromSuccess();
        if (guildConfig.PrivateFeedbackChannel is 0)
            return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        Messages.Culture = guildConfig.GetCulture();
        var i = Random.Shared.Next(1, 4);

        var embed = new EmbedBuilder()
            .WithTitle($"Beep{i}".Localized())
            .WithDescription(Messages.Ready)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(ColorsList.Blue)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfig.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built }, ct: ct);
    }
}

/// <summary>
///     Handles logging the contents of a deleted message and the user who deleted the message
///     to a guild's <see cref="GuildConfiguration.PrivateFeedbackChannel" /> if one is set.
/// </summary>
public class MessageDeletedResponder : IResponder<IMessageDelete> {
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly IDiscordRestChannelAPI  _channelApi;
    private readonly GuildDataService        _dataService;
    private readonly IDiscordRestUserAPI     _userApi;

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

        Messages.Culture = guildConfiguration.GetCulture();

        var embed = new EmbedBuilder()
            .WithSmallTitle(
                string.Format(
                    Messages.CachedMessageDeleted,
                    message.Author.GetTag()), message.Author)
            .WithDescription(
                $"{Mention.Channel(gatewayEvent.ChannelID)}\n{Markdown.BlockCode(message.Content.SanitizeForBlockCode())}")
            .WithActionFooter(user)
            .WithTimestamp(message.Timestamp)
            .WithColour(ColorsList.Red)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

/// <summary>
///     Handles logging the difference between an edited message's old and new content
///     to a guild's <see cref="GuildConfiguration.PrivateFeedbackChannel" /> if one is set.
/// </summary>
public class MessageEditedResponder : IResponder<IMessageUpdate> {
    private readonly CacheService           _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService       _dataService;
    private readonly IDiscordRestUserAPI    _userApi;

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
            return Result.FromSuccess(); // The message wasn't actually edited

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

        Messages.Culture = guildConfiguration.GetCulture();

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.CachedMessageEdited, message.Author.GetTag()), message.Author)
            .WithDescription($"https://discord.com/channels/{guildId}/{channelId}/{messageId}\n{diff.AsMarkdown()}")
            .WithUserFooter(currentUser)
            .WithTimestamp(timestamp.Value)
            .WithColour(ColorsList.Yellow)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

/// <summary>
///     Handles sending a guild's <see cref="GuildConfiguration.WelcomeMessage" /> if one is set.
/// </summary>
/// <seealso cref="GuildConfiguration.WelcomeMessage" />
public class GuildMemberAddResponder : IResponder<IGuildMemberAdd> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService       _dataService;
    private readonly IDiscordRestGuildAPI   _guildApi;

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

        Messages.Culture = guildConfiguration.GetCulture();
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
            .WithColour(ColorsList.Green)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildConfiguration.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

/// <summary>
///     Handles sending a notification when a scheduled event has been cancelled
///     in a guild's <see cref="GuildConfiguration.EventNotificationChannel" /> if one is set.
/// </summary>
public class GuildScheduledEventDeleteResponder : IResponder<IGuildScheduledEventDelete> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService       _dataService;

    public GuildScheduledEventDeleteResponder(IDiscordRestChannelAPI channelApi, GuildDataService dataService) {
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
            .WithColour(ColorsList.Red)
            .WithCurrentTimestamp()
            .Build();

        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            guildData.Configuration.EventNotificationChannel.ToDiscordSnowflake(), embeds: new[] { built }, ct: ct);
    }
}

/// <summary>
///     Handles updating <see cref="MemberData.Roles" /> when a guild member is updated.
/// </summary>
public class GuildMemberUpdateResponder : IResponder<IGuildMemberUpdate> {
    private readonly GuildDataService _dataService;

    public GuildMemberUpdateResponder(GuildDataService dataService) {
        _dataService = dataService;
    }

    public async Task<Result> RespondAsync(IGuildMemberUpdate gatewayEvent, CancellationToken ct = default) {
        var memberData = await _dataService.GetMemberData(gatewayEvent.GuildID, gatewayEvent.User.ID, ct);
        memberData.Roles = gatewayEvent.Roles.ToList();
        return Result.FromSuccess();
    }
}

/// <summary>
///     Handles sending replies to easter egg messages.
/// </summary>
public class MessageCreateResponder : IResponder<IMessageCreate> {
    private readonly IDiscordRestChannelAPI _channelApi;

    public MessageCreateResponder(IDiscordRestChannelAPI channelApi) {
        _channelApi = channelApi;
    }

    public Task<Result> RespondAsync(IMessageCreate gatewayEvent, CancellationToken ct = default) {
        _ = _channelApi.CreateMessageAsync(
            gatewayEvent.ChannelID, ct: ct, content: gatewayEvent.Content switch {
                "whoami"  => "`nobody`",
                "сука !!" => "`root`",
                "воооо"   => "`removing /...`",
                "пон" =>
                    "https://cdn.discordapp.com/attachments/837385840946053181/1087236080950055023/vUORS10xPaY-1.jpg",
                "++++" => "#",
                _      => default(Optional<string>)
            });
        return Task.FromResult(Result.FromSuccess());
    }
}
