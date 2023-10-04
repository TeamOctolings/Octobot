using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Octobot.Data;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

/// <summary>
///     Handles sending a <see cref="Ready" /> message to a guild that has just initialized if that guild
///     has <see cref="GuildSettings.ReceiveStartupMessages" /> enabled
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

    public async Task<Result> RespondAsync(IGuildDelete gatewayEvent, CancellationToken ct = default)
    {
        var guildId = gatewayEvent.ID;
        await _guildData.RemoveGuildId(guildId);
        _logger.LogInformation("Left guild {guildId}", guildId);

        return Result.FromSuccess();
    }
}
