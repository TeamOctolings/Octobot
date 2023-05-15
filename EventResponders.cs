using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Boyfriend;

public class ReadyResponder : IResponder<IGuildCreate> {
    private readonly IDiscordRestChannelAPI _channelApi;

    public ReadyResponder(IDiscordRestChannelAPI channelApi) {
        _channelApi = channelApi;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.Guild.IsT0) return Result.FromSuccess(); // is IAvailableGuild

        var guild = gatewayEvent.Guild.AsT0;
        if (guild.GetConfigBool("SendReadyMessages").IsDefined(out var enabled)
            && enabled
            && guild.GetChannel("PrivateFeedbackChannel").IsDefined(out var channel)) {
            Messages.Culture = guild.GetCulture();
            var i = Random.Shared.Next(1, 4);

            return (Result)await _channelApi.CreateMessageAsync(
                channel.ID, string.Format(Messages.Ready, Boyfriend.GetLocalized($"Beep{i}")), ct: ct);
        }

        return Result.FromSuccess();
    }
}
