using Microsoft.Extensions.Hosting;

namespace Boyfriend.Services;

public sealed class BackgroundGuildDataSaverService : BackgroundService
{
    private readonly GuildDataService _guildData;

    public BackgroundGuildDataSaverService(GuildDataService guildData)
    {
        _guildData = guildData;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await _guildData.SaveAsync(ct);
        }
    }
}
