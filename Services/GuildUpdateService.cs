using Boyfriend.Services.Data;
using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;

namespace Boyfriend.Services;

public class GuildUpdateService : BackgroundService {
    private readonly GuildDataService     _dataService;
    private readonly IDiscordRestGuildAPI _guildApi;

    public GuildUpdateService(GuildDataService dataService, IDiscordRestGuildAPI guildApi) {
        _dataService = dataService;
        _guildApi = guildApi;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var tasks = new List<Task>();

        while (await timer.WaitForNextTickAsync(ct)) {
            foreach (var id in _dataService.GetGuildIds())
                tasks.Add(TickGuildAsync(id, ct));

            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    private async Task TickGuildAsync(Snowflake guildId, CancellationToken ct = default) {
        var data = await _dataService.GetData(guildId, ct);
        Messages.Culture = data.Culture;

        foreach (var memberData in data.MemberData.Values)
            if (DateTimeOffset.UtcNow > memberData.BannedUntil) {
                _ = _guildApi.RemoveGuildBanAsync(
                    guildId, memberData.Id.ToDiscordSnowflake(), Messages.PunishmentExpired.EncodeHeader(), ct);
                memberData.BannedUntil = null;
            }
    }
}
