using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Services.Update;

public sealed class GuildUpdateService : BackgroundService
{
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly ILogger<GuildUpdateService> _logger;

    public GuildUpdateService(IDiscordRestGuildAPI guildApi,
        GuildDataService guildData, ILogger<GuildUpdateService> logger)
    {
        _guildApi = guildApi;
        _guildData = guildData;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var guildIds = _guildData.GetGuildIds();
            foreach (var id in guildIds)
            {
                var tickResult = await TickGuildsAsync(id, ct);
                _logger.LogResult(tickResult, $"Error in guild update for guild {id}.");
            }
        }
    }

    private async Task<Result> TickGuildsAsync(Snowflake guildId, CancellationToken ct)
    {
        var getGuildResult = await _guildApi.GetGuildAsync(guildId, ct: ct);
        if (getGuildResult.IsSuccess)
        {
            return Result.FromSuccess();
        }

        await _guildData.RemoveGuildId(guildId);
        _logger.LogInformation("Left guild {guildId}", guildId);

        return Result.FromSuccess();
    }
}
