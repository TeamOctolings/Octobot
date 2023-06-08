using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Services;

public class UtilityService : IHostedService {
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly IDiscordRestUserAPI _userApi;

    public UtilityService(IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi) {
        _guildApi = guildApi;
        _userApi = userApi;
    }

    public Task StartAsync(CancellationToken ct) {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) {
        return Task.CompletedTask;
    }

    public async Task<Result<string?>> CheckInteractionsAsync(
        Snowflake guildId, Snowflake interacterId, Snowflake targetId, string action, CancellationToken ct = default) {
        if (interacterId == targetId)
            return Result<string?>.FromSuccess($"UserCannot{action}Themselves".Localized());

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: ct);
        if (!guildResult.IsDefined(out var guild))
            return Result<string?>.FromError(guildResult);

        if (targetId == guild.OwnerID) return Result<string?>.FromSuccess($"UserCannot{action}Owner".Localized());

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result<string?>.FromError(currentUserResult);

        if (currentUser.ID == targetId)
            return Result<string?>.FromSuccess($"UserCannot{action}Bot".Localized());

        if (interacterId == guild.OwnerID) return Result<string?>.FromSuccess(null);

        var targetMemberResult = await _guildApi.GetGuildMemberAsync(guildId, targetId, ct);
        if (!targetMemberResult.IsDefined(out var targetMember))
            return Result<string?>.FromSuccess(null);

        var currentMemberResult = await _guildApi.GetGuildMemberAsync(guildId, currentUser.ID, ct);
        if (!currentMemberResult.IsDefined(out var currentMember))
            return Result<string?>.FromError(currentMemberResult);

        var rolesResult = await _guildApi.GetGuildRolesAsync(guildId, ct);
        if (!rolesResult.IsDefined(out var roles))
            return Result<string?>.FromError(rolesResult);

        var interacterResult = await _guildApi.GetGuildMemberAsync(guildId, interacterId, ct);
        if (!interacterResult.IsDefined(out var interacter))
            return Result<string?>.FromError(interacterResult);

        var targetRoles = roles.Where(r => targetMember.Roles.Contains(r.ID));
        var interacterRoles = roles.Where(r => interacter.Roles.Contains(r.ID));
        var botRoles = roles.Where(r => currentMember.Roles.Contains(r.ID));

        var targetRolesList = targetRoles.ToList();
        var targetBotRoleDiff = targetRolesList.MaxOrDefault(r => r.Position) - botRoles.Max(r => r.Position);
        var targetInteracterRoleDiff
            = targetRolesList.MaxOrDefault(r => r.Position) - interacterRoles.Max(r => r.Position);

        if (targetInteracterRoleDiff >= 0)
            return Result<string?>.FromSuccess($"UserCannot{action}Target".Localized());

        if (targetBotRoleDiff >= 0)
            return Result<string?>.FromSuccess($"BotCannot{action}Target".Localized());

        return Result<string?>.FromSuccess(null);
    }
}
