using System.Drawing;
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
using Remora.Results;

// ReSharper disable UnusedType.Global

namespace Boyfriend;

public class GuildCreateResponder : IResponder<IGuildCreate> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestUserAPI _userApi;

    public GuildCreateResponder(IDiscordRestChannelAPI channelApi, IDiscordRestUserAPI userApi) {
        _channelApi = channelApi;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.Guild.IsT0) return Result.FromSuccess(); // is IAvailableGuild

        var guild = gatewayEvent.Guild.AsT0;
        Boyfriend.Logger.LogInformation("Joined guild \"{Name}\"", guild.Name);

        var channelResult = guild.ID.GetConfigChannel("PrivateFeedbackChannel");
        if (!channelResult.IsDefined(out var channel)) return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        if (!guild.GetConfigBool("ReceiveStartupMessages").IsDefined(out var shouldSendStartupMessage)
            || !shouldSendStartupMessage) return Result.FromSuccess();

        Messages.Culture = guild.ID.GetGuildCulture();
        var i = Random.Shared.Next(1, 4);

        var embed = new EmbedBuilder()
            .WithTitle(Boyfriend.GetLocalized($"Beep{i}"))
            .WithDescription(Messages.Ready)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(Color.Aqua)
            .Build();

        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            channel, embeds: new[] { built }!, ct: ct);
    }
}

public class MessageDeletedResponder : IResponder<IMessageDelete> {
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;

    public MessageDeletedResponder(
        IDiscordRestAuditLogAPI auditLogApi, CacheService cacheService, IDiscordRestChannelAPI channelApi) {
        _auditLogApi = auditLogApi;
        _cacheService = cacheService;
        _channelApi = channelApi;
    }

    public async Task<Result> RespondAsync(IMessageDelete gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId)) return Result.FromSuccess();

        var channelResult = guildId.GetConfigChannel("PrivateFeedbackChannel");
        if (!channelResult.IsDefined(out var logChannel)) return Result.FromSuccess();

        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            new KeyHelpers.MessageCacheKey(gatewayEvent.ChannelID, gatewayEvent.ID), ct);
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
            var userResult = await _cacheService.TryGetValueAsync<IUser>(
                new KeyHelpers.UserCacheKey(auditLog.UserID!.Value), ct);
            if (!userResult.IsDefined(out user)) return Result.FromError(userResult);
        }

        Messages.Culture = guildId.GetGuildCulture();
        var embed = new EmbedBuilder()
            .WithSmallTitle(
                message.Author,
                string.Format(
                    Messages.CachedMessageDeleted,
                    message.Author.GetTag()))
            .WithDescription(
                $"{Mention.Channel(gatewayEvent.ChannelID)}\n{Markdown.BlockCode(message.Content.SanitizeForBlockCode())}")
            .WithActionFooter(user)
            .WithTimestamp(message.Timestamp)
            .WithColour(Color.Crimson)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            logChannel, embeds: new[] { built }, allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

public class MessageEditedResponder : IResponder<IMessageUpdate> {
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;

    public MessageEditedResponder(CacheService cacheService, IDiscordRestChannelAPI channelApi) {
        _cacheService = cacheService;
        _channelApi = channelApi;
    }

    public async Task<Result> RespondAsync(IMessageUpdate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId)) return Result.FromSuccess();
        if (!gatewayEvent.ChannelID.IsDefined(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ChannelID)));
        if (!gatewayEvent.ID.IsDefined(out var messageId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ID)));
        if (!gatewayEvent.Content.IsDefined(out var newContent))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.Content)));
        if (!gatewayEvent.EditedTimestamp.IsDefined(out var timestamp))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.EditedTimestamp)));

        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            new KeyHelpers.MessageCacheKey(channelId, messageId), ct);
        if (!messageResult.IsDefined(out var message)) return Result.FromError(messageResult);
        if (string.IsNullOrWhiteSpace(message.Content)
            || string.IsNullOrWhiteSpace(newContent)
            || message.Content == newContent) return Result.FromSuccess();

        var logChannelResult = guildId.GetConfigChannel("PrivateFeedbackChannel");
        if (!logChannelResult.IsDefined(out var logChannel)) return Result.FromSuccess();

        var currentUserResult = await _cacheService.TryGetValueAsync<IUser>(
            new KeyHelpers.CurrentUserCacheKey(), ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        var diff = new SideBySideDiffBuilder(Differ.Instance).BuildDiffModel(message.Content, newContent, true, true);

        Messages.Culture = guildId.GetGuildCulture();
        var embed = new EmbedBuilder()
            .WithSmallTitle(
                message.Author,
                string.Format(Messages.CachedMessageEdited, message.Author.GetTag()),
                $"https://discord.com/channels/{guildId}/{channelId}/{messageId}")
            .WithDescription($"{Mention.Channel(message.ChannelID)}\n{diff.AsMarkdown()}")
            .WithUserFooter(currentUser)
            .WithTimestamp(timestamp.Value)
            .WithColour(Color.Gold)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            logChannel, embeds: new[] { built }, allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}

public class GuildMemberAddResponder : IResponder<IGuildMemberAdd> {
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;

    public GuildMemberAddResponder(CacheService cacheService, IDiscordRestChannelAPI channelApi) {
        _cacheService = cacheService;
        _channelApi = channelApi;
    }

    public async Task<Result> RespondAsync(IGuildMemberAdd gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.GetConfigString("WelcomeMessage").IsDefined(out var welcomeMessage)
            || welcomeMessage is "off" or "disable" or "disabled")
            return Result.FromSuccess();
        if (welcomeMessage is "default" or "reset") {
            Messages.Culture = gatewayEvent.GuildID.GetGuildCulture();
            welcomeMessage = Messages.DefaultWelcomeMessage;
        }

        if (!gatewayEvent.GuildID.GetConfigChannel("PublicFeedbackChannel").IsDefined(out var channel))
            return Result.FromSuccess();
        if (!gatewayEvent.User.IsDefined(out var user))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.User)));

        var guildResult = await _cacheService.TryGetValueAsync<IGuild>(
            new KeyHelpers.GuildCacheKey(gatewayEvent.GuildID), ct);
        if (!guildResult.IsDefined(out var guild)) return Result.FromError(guildResult);

        var embed = new EmbedBuilder()
            .WithSmallTitle(user, string.Format(welcomeMessage, user.GetTag(), guild.Name))
            .WithGuildFooter(guild)
            .WithTimestamp(gatewayEvent.JoinedAt)
            .WithColour(Color.LawnGreen)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            channel, embeds: new[] { built }, allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}
