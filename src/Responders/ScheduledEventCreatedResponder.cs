using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

/// <summary>
///     Handles adding a scheduled event to a guild's ScheduledEventData.
/// </summary>
[UsedImplicitly]
public class ScheduledEventCreatedResponder : IResponder<IGuildScheduledEventCreate>
{
    private readonly GuildDataService _guildData;

    public ScheduledEventCreatedResponder(GuildDataService guildData)
    {
        _guildData = guildData;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventCreate gatewayEvent, CancellationToken ct = default)
    {
        var data = await _guildData.GetData(gatewayEvent.GuildID, ct);
        data.ScheduledEvents.Add(gatewayEvent.ID.Value,
            new ScheduledEventData(gatewayEvent.ID.Value,
                gatewayEvent.Name, gatewayEvent.ScheduledStartTime, gatewayEvent.Status));

        return Result.FromSuccess();
    }
}
