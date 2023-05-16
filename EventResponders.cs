using System.Drawing;
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

        var channelResult = guild.ID.GetChannel("PrivateFeedbackChannel");
        if (!channelResult.IsDefined(out var channel)) return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        if (guild.GetConfigBool("ReceiveStartupMessages").IsDefined(out var shouldSendStartupMessage)
            && shouldSendStartupMessage) {
            Messages.Culture = guild.GetCulture();
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

        return Result.FromSuccess();
    }
}

public class MessageDeletedResponder : IResponder<IMessageDelete> {
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestUserAPI _userApi;

    public MessageDeletedResponder(
        IDiscordRestChannelAPI  channelApi, IDiscordRestUserAPI userApi, CacheService cacheService,
        IDiscordRestAuditLogAPI auditLogApi) {
        _channelApi = channelApi;
        _userApi = userApi;
        _cacheService = cacheService;
        _auditLogApi = auditLogApi;
    }

    public async Task<Result> RespondAsync(IMessageDelete gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId)) return Result.FromSuccess();

        var channelResult = guildId.GetChannel("PrivateFeedbackChannel");
        if (!channelResult.IsDefined(out var channel)) return Result.FromSuccess();

        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            new KeyHelpers.MessageCacheKey(gatewayEvent.ChannelID, gatewayEvent.ID), ct);
        if (messageResult.IsDefined(out var message)) {
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

            var embed = new EmbedBuilder()
                .WithAuthor(string.Format(Messages.CachedMessageDeleted, message.Author))
                .WithTitle(
                    message.Author,
                    string.Format(
                        Messages.CachedMessageDeleted,
                        $"{message.Author.Username}#{message.Author.Discriminator:0000}"))
                .WithDescription(Markdown.BlockCode(message.Content.SanitizeForBlockCode()))
                .WithActionFooter(user)
                .WithTimestamp(message.Timestamp)
                .WithColour(Color.Crimson)
                .Build();

            if (!embed.IsDefined(out var built)) return Result.FromError(embed);

            return (Result)await _channelApi.CreateMessageAsync(
                channel, embeds: new[] { built }, allowedMentions: Boyfriend.NoMentions, ct: ct);
        }

        return (Result)messageResult;
    }
}
