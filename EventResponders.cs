using System.Drawing;
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
            channel, embeds: new[] { built }, ct: ct);
    }
}

public class MessageDeletedResponder : IResponder<IMessageDelete> {
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestUserAPI _userApi;

    public MessageDeletedResponder(
        IDiscordRestAuditLogAPI auditLogApi, CacheService cacheService, IDiscordRestChannelAPI channelApi,
        IDiscordRestUserAPI     userApi) {
        _auditLogApi = auditLogApi;
        _cacheService = cacheService;
        _channelApi = channelApi;
        _userApi = userApi;
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
            var userResult = await auditLog.UserID!.Value.TryGetUserAsync(_cacheService, _userApi, ct);
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
        if (!gatewayEvent.GuildID.IsDefined(out var guildId))
            return Result.FromSuccess();
        if (!gatewayEvent.Content.IsDefined(out var newContent))
            return Result.FromSuccess();

        if (!gatewayEvent.ChannelID.IsDefined(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ChannelID)));
        if (!gatewayEvent.ID.IsDefined(out var messageId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.ID)));
        if (!gatewayEvent.EditedTimestamp.IsDefined(out var timestamp))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.EditedTimestamp)));

        var cacheKey = new KeyHelpers.MessageCacheKey(channelId, messageId);
        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            cacheKey, ct);
        if (!messageResult.IsDefined(out var message)) return Result.FromError(messageResult);
        if (string.IsNullOrWhiteSpace(message.Content)
            || string.IsNullOrWhiteSpace(newContent)
            || message.Content == newContent) return Result.FromSuccess();

        await _cacheService.EvictAsync<IMessage>(cacheKey, ct);
        var newMessageResult = await _channelApi.GetChannelMessageAsync(channelId, messageId, ct);
        if (!newMessageResult.IsDefined(out var newMessage)) return Result.FromError(newMessageResult);
        // No need to await the recache since we don't depend on it
        _ = _cacheService.CacheAsync(cacheKey, newMessage, ct);

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
                string.Format(Messages.CachedMessageEdited, message.Author.GetTag()))
            .WithDescription($"https://discord.com/channels/{guildId}/{channelId}/{messageId}\n{diff.AsMarkdown()}")
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

public class GuildScheduledEventCreateResponder : IResponder<IGuildScheduledEventCreate> {
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestUserAPI _userApi;

    public GuildScheduledEventCreateResponder(
        CacheService cacheService, IDiscordRestChannelAPI channelApi, IDiscordRestUserAPI userApi) {
        _cacheService = cacheService;
        _channelApi = channelApi;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventCreate gatewayEvent, CancellationToken ct = default) {
        var channelResult = gatewayEvent.GuildID.GetConfigChannel("EventNotificationChannel");
        if (!channelResult.IsDefined(out var channel)) return Result.FromSuccess();

        var currentUserResult = await _cacheService.TryGetValueAsync<IUser>(
            new KeyHelpers.CurrentUserCacheKey(), ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        if (!gatewayEvent.CreatorID.IsDefined(out var creatorId))
            return Result.FromError(new ArgumentNullError(nameof(gatewayEvent.CreatorID)));
        var creatorResult = await creatorId.Value.TryGetUserAsync(_cacheService, _userApi, ct);
        if (!creatorResult.IsDefined(out var creator)) return Result.FromError(creatorResult);

        Messages.Culture = gatewayEvent.GuildID.GetGuildCulture();

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
                        Messages.LocalEventCreatedDescription,
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
                        Messages.ExternalEventCreatedDescription,
                        Markdown.Timestamp(gatewayEvent.ScheduledStartTime),
                        Markdown.Timestamp(endTime),
                        Markdown.InlineCode(location)
                    ))}";
                break;
            default:
                return Result.FromError(new ArgumentOutOfRangeError(nameof(gatewayEvent.EntityType)));
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(creator, string.Format(Messages.EventCreatedTitle, creator.GetTag()))
            .WithTitle(gatewayEvent.Name)
            .WithDescription(embedDescription)
            .WithEventCover(gatewayEvent.ID, gatewayEvent.Image)
            .WithUserFooter(currentUser)
            .WithCurrentTimestamp()
            .WithColour(Color.Gray)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        var button = new ButtonComponent(
            ButtonComponentStyle.Primary,
            Messages.EventDetailsButton,
            new PartialEmoji(Name: "📋"),
            CustomIDHelpers.CreateButtonIDWithState(
                "scheduled-event-details", $"{gatewayEvent.GuildID}:{gatewayEvent.ID}")
        );

        return (Result)await _channelApi.CreateMessageAsync(
            channel, embeds: new[] { built }, components: new[] { new ActionRowComponent(new[] { button }) }, ct: ct);
    }
}
