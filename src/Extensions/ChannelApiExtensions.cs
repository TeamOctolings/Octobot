using OneOf;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Extensions;

public static class ChannelApiExtensions
{
    public static async Task<Result> CreateMessageResultAsync(this IDiscordRestChannelAPI channelApi,
        Snowflake channelId, Optional<string> message = default, Optional<string> nonce = default,
        Optional<bool> isTextToSpeech = default, Optional<IReadOnlyList<IEmbed>> embeds = default,
        Optional<IAllowedMentions> allowedMentions = default, Optional<IMessageReference> messageRefenence = default,
        Optional<IReadOnlyList<IMessageComponent>> components = default,
        Optional<IReadOnlyList<Snowflake>> stickerIds = default,
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>> attachments = default,
        Optional<MessageFlags> flags = default, CancellationToken ct = default)
    {
        return (Result)await channelApi.CreateMessageAsync(channelId, message, nonce, isTextToSpeech, embeds,
            allowedMentions, messageRefenence, components, stickerIds, attachments, flags, ct);
    }
}
