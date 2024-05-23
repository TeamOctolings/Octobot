using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Extensions;

namespace TeamOctolings.Octobot.Services;

public sealed class AccessControlService
{
    private readonly GuildDataService _data;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly IDiscordRestUserAPI _userApi;

    public AccessControlService(GuildDataService data, IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi)
    {
        _data = data;
        _guildApi = guildApi;
        _userApi = userApi;
    }

    private static bool CheckPermission(IEnumerable<IRole> roles, GuildData data, MemberData memberData,
        DiscordPermission permission)
    {
        var moderatorRole = GuildSettings.ModeratorRole.Get(data.Settings);
        if (!moderatorRole.Empty() && memberData.Roles.Contains(moderatorRole.Value))
        {
            return true;
        }

        return roles
            .Where(r => memberData.Roles.Contains(r.ID.Value))
            .Any(r =>
                r.Permissions.HasPermission(permission)
            );
    }

    /// <summary>
    ///     Checks whether or not a member can interact with another member
    /// </summary>
    /// <param name="guildId">The ID of the guild in which an operation is being performed.</param>
    /// <param name="interacterId">The executor of the operation.</param>
    /// <param name="targetId">The target of the operation.</param>
    /// <param name="action">The operation.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>
    ///     <list type="bullet">
    ///         <item>A result which has succeeded with a null string if the member can interact with the target.</item>
    ///         <item>
    ///             A result which has succeeded with a non-null string containing the error message if the member cannot
    ///             interact with the target.
    ///         </item>
    ///         <item>A result which has failed if an error occurred during the execution of this method.</item>
    ///     </list>
    /// </returns>
    public async Task<Result<string?>> CheckInteractionsAsync(
        Snowflake guildId, Snowflake? interacterId, Snowflake targetId, string action, CancellationToken ct = default)
    {
        if (interacterId == targetId)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Themselves".Localized());
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: ct);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result<string?>.FromError(guildResult);
        }

        if (interacterId == guild.OwnerID)
        {
            return Result<string?>.FromSuccess(null);
        }

        var botResult = await _userApi.GetCurrentUserAsync(ct);
        if (!botResult.IsDefined(out var bot))
        {
            return Result<string?>.FromError(botResult);
        }

        var rolesResult = await _guildApi.GetGuildRolesAsync(guildId, ct);
        if (!rolesResult.IsDefined(out var roles))
        {
            return Result<string?>.FromError(rolesResult);
        }

        var data = await _data.GetData(guildId, ct);
        var targetData = data.GetOrCreateMemberData(targetId);
        var botData = data.GetOrCreateMemberData(bot.ID);

        if (interacterId is null)
        {
            return CheckInteractions(action, guild, roles, targetData, botData, botData);
        }

        var interacterData = data.GetOrCreateMemberData(interacterId.Value);
        var hasPermission = CheckPermission(roles, data, interacterData,
            action switch
            {
                "Ban" => DiscordPermission.BanMembers,
                "Kick" => DiscordPermission.KickMembers,
                "Mute" or "Unmute" => DiscordPermission.ModerateMembers,
                _ => throw new Exception()
            });

        return hasPermission
            ? CheckInteractions(action, guild, roles, targetData, botData, interacterData)
            : Result<string?>.FromSuccess($"UserCannot{action}Members".Localized());
    }

    private static Result<string?> CheckInteractions(
        string action, IGuild guild, IReadOnlyList<IRole> roles, MemberData targetData, MemberData botData,
        MemberData interacterData)
    {
        if (targetData.Id == guild.OwnerID)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Owner".Localized());
        }

        var targetRoles = roles.Where(r => targetData.Roles.Contains(r.ID.Value)).ToList();
        var botRoles = roles.Where(r => botData.Roles.Contains(r.ID.Value));

        var targetBotRoleDiff = targetRoles.MaxOrDefault(r => r.Position) - botRoles.MaxOrDefault(r => r.Position);
        if (targetBotRoleDiff >= 0)
        {
            return Result<string?>.FromSuccess($"BotCannot{action}Target".Localized());
        }

        var interacterRoles = roles.Where(r => interacterData.Roles.Contains(r.ID.Value));
        var targetInteracterRoleDiff
            = targetRoles.MaxOrDefault(r => r.Position) - interacterRoles.MaxOrDefault(r => r.Position);
        return targetInteracterRoleDiff < 0
            ? Result<string?>.FromSuccess(null)
            : Result<string?>.FromSuccess($"UserCannot{action}Target".Localized());
    }
}
