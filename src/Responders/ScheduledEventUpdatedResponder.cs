using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Boyfriend.Responders;

[UsedImplicitly]
public class ScheduledEventUpdatedResponder : IResponder<IGuildScheduledEventUpdate>
{
    private readonly GuildDataService _guildData;

    public ScheduledEventUpdatedResponder(GuildDataService guildData)
    {
        _guildData = guildData;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventUpdate gatewayEvent, CancellationToken ct = default)
    {
        var data = await _guildData.GetData(gatewayEvent.GuildID, ct);
        var eventData = data.ScheduledEvents[gatewayEvent.ID.Value];
        eventData.Name = gatewayEvent.Name;
        eventData.ScheduledStartTime = gatewayEvent.ScheduledStartTime;
        eventData.ScheduleOnStatusUpdated = eventData.Status != gatewayEvent.Status;
        eventData.Status = gatewayEvent.Status;

        return Result.FromSuccess();
    }
}
