using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Responders;

/// <summary>
///     Handles sending replies to easter egg messages.
/// </summary>
[UsedImplicitly]
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
                "осу"  => "https://github.com/ppy/osu",
                _      => default(Optional<string>)
            });
        return Task.FromResult(Result.FromSuccess());
    }
}
