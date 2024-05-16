using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Services;

namespace TeamOctolings.Octobot.Responders;

/// <summary>
///     Handles removing guild ID from <see cref="GuildData" /> if the guild becomes unavailable.
/// </summary>
[UsedImplicitly]
public class GuildUnloadedResponder : IResponder<IGuildDelete>
{
    private readonly GuildDataService _guildData;
    private readonly ILogger<GuildUnloadedResponder> _logger;

    public GuildUnloadedResponder(
        GuildDataService guildData, ILogger<GuildUnloadedResponder> logger)
    {
        _guildData = guildData;
        _logger = logger;
    }

    public Task<Result> RespondAsync(IGuildDelete gatewayEvent, CancellationToken ct = default)
    {
        var guildId = gatewayEvent.ID;
        var isDataRemoved = _guildData.UnloadGuildData(guildId);
        if (isDataRemoved)
        {
            _logger.LogInformation("Unloaded guild {GuildId}", guildId);
        }

        return Task.FromResult(Result.Success);
    }
}
