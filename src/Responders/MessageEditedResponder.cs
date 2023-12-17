using System.Text;
using DiffPlex.DiffBuilder;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching;
using Remora.Discord.Caching.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

/// <summary>
///     Handles logging the difference between an edited message's old and new content
///     to a guild's <see cref="GuildSettings.PrivateFeedbackChannel" /> if one is set.
/// </summary>
[UsedImplicitly]
public class MessageEditedResponder : IResponder<IMessageUpdate>
{
    private readonly CacheService _cacheService;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _guildData;

    public MessageEditedResponder(
        CacheService cacheService, IDiscordRestChannelAPI channelApi, GuildDataService guildData)
    {
        _cacheService = cacheService;
        _channelApi = channelApi;
        _guildData = guildData;
    }

    public async Task<Result> RespondAsync(IMessageUpdate gatewayEvent, CancellationToken ct = default)
    {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId))
        {
            return Result.FromSuccess();
        }

        var cfg = await _guildData.GetSettings(guildId, ct);
        if (GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty())
        {
            return Result.FromSuccess();
        }

        if (!gatewayEvent.Content.IsDefined(out var newContent))
        {
            return Result.FromSuccess();
        }

        if (!gatewayEvent.EditedTimestamp.IsDefined(out var timestamp))
        {
            return Result.FromSuccess(); // The message wasn't actually edited
        }

        if (!gatewayEvent.ChannelID.IsDefined(out var channelId))
        {
            return new ArgumentNullError(nameof(gatewayEvent.ChannelID));
        }

        if (!gatewayEvent.ID.IsDefined(out var messageId))
        {
            return new ArgumentNullError(nameof(gatewayEvent.ID));
        }

        if (gatewayEvent.Author.IsDefined(out var author) && author.IsBot.OrDefault(false))
        {
            return Result.FromSuccess();
        }

        var cacheKey = new KeyHelpers.MessageCacheKey(channelId, messageId);
        var messageResult = await _cacheService.TryGetValueAsync<IMessage>(
            cacheKey, ct);
        if (!messageResult.IsDefined(out var message))
        {
            _ = _channelApi.GetChannelMessageAsync(channelId, messageId, ct);
            return Result.FromSuccess();
        }

        if (message.Content == newContent)
        {
            return Result.FromSuccess();
        }

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

        var diff = InlineDiffBuilder.Diff(message.Content, newContent);

        Messages.Culture = GuildSettings.Language.Get(cfg);

        var builder = new StringBuilder().AppendLine(
                string.Format(Messages.DescriptionActionJumpToMessage,
                    $"https://discord.com/channels/{guildId}/{channelId}/{messageId}"))
            .AppendLine(diff.AsMarkdown());

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.CachedMessageEdited, message.Author.GetTag()), message.Author)
            .WithDescription(builder.ToString())
            .WithTimestamp(timestamp.Value)
            .WithColour(ColorsList.Yellow)
            .Build();

        return await _channelApi.CreateMessageWithEmbedResultAsync(
            GuildSettings.PrivateFeedbackChannel.Get(cfg), embedResult: embed,
            allowedMentions: Octobot.NoMentions, ct: ct);
    }
}
