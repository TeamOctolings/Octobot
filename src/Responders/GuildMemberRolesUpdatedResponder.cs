using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

/// <summary>
///     Handles updating <see cref="MemberData.Roles" /> when a guild member is updated.
/// </summary>
[UsedImplicitly]
public class GuildMemberUpdateResponder : IResponder<IGuildMemberUpdate>
{
    private readonly GuildDataService _guildData;

    public GuildMemberUpdateResponder(GuildDataService guildData)
    {
        _guildData = guildData;
    }

    public async Task<Result> RespondAsync(IGuildMemberUpdate gatewayEvent, CancellationToken ct = default)
    {
        var memberData = await _guildData.GetMemberData(gatewayEvent.GuildID, gatewayEvent.User.ID, ct);
        if (memberData.MutedUntil is null)
        {
            memberData.Roles = gatewayEvent.Roles.ToList().ConvertAll(r => r.Value);
        }

        return Result.FromSuccess();
    }
}
