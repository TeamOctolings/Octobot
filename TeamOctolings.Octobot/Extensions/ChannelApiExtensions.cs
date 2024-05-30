using OneOf;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;
using Remora.Results;

namespace TeamOctolings.Octobot.Extensions;

public static class ChannelApiExtensions
{
    public static async Task<Result> CreateMessageWithEmbedResultAsync(this IDiscordRestChannelAPI channelApi,
        Snowflake channelId, Optional<string> message = default, Optional<string> nonce = default,
        Optional<bool> isTextToSpeech = default, Optional<Result<Embed>> embedResult = default,
        Optional<IAllowedMentions> allowedMentions = default, Optional<IMessageReference> messageReference = default,
        Optional<IReadOnlyList<IMessageComponent>> components = default,
        Optional<IReadOnlyList<Snowflake>> stickerIds = default,
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>> attachments = default,
        Optional<MessageFlags> flags = default, Optional<bool> enforceNonce = default,
        Optional<IPollCreateRequest> poll = default, CancellationToken ct = default)
    {
        if (!embedResult.IsDefined() || !embedResult.Value.IsDefined(out var embed))
        {
            return ResultExtensions.FromError(embedResult.Value);
        }

        return (Result)await channelApi.CreateMessageAsync(channelId, message, nonce, isTextToSpeech, new[] { embed },
            allowedMentions, messageReference, components, stickerIds, attachments, flags, enforceNonce, poll, ct);
    }
}
