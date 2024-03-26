using Octobot.Data;
using Octobot.Extensions;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Services;

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

    private static bool CheckPermission(IEnumerable<IRole> roles, GuildData data, Snowflake memberId,
        IGuildMember member,
        DiscordPermission permission)
    {
        var moderatorRole = GuildSettings.ModeratorRole.Get(data.Settings);
        if (!moderatorRole.Empty() && data.GetOrCreateMemberData(memberId).Roles.Contains(moderatorRole.Value))
        {
            return true;
        }

        return roles
            .Where(r => member.Roles.Contains(r.ID))
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

        var botMemberResult = await _guildApi.GetGuildMemberAsync(guildId, bot.ID, ct);
        if (!botMemberResult.IsDefined(out var botMember))
        {
            return Result<string?>.FromError(botMemberResult);
        }

        var targetMemberResult = await _guildApi.GetGuildMemberAsync(guildId, targetId, ct);
        if (!targetMemberResult.IsDefined(out var targetMember))
        {
            return Result<string?>.FromSuccess(null);
        }

        var rolesResult = await _guildApi.GetGuildRolesAsync(guildId, ct);
        if (!rolesResult.IsDefined(out var roles))
        {
            return Result<string?>.FromError(rolesResult);
        }

        if (interacterId is null)
        {
            return CheckInteractions(action, guild, roles, targetMember, botMember, botMember);
        }

        var interacterResult = await _guildApi.GetGuildMemberAsync(guildId, interacterId.Value, ct);
        if (!interacterResult.IsDefined(out var interacter))
        {
            return Result<string?>.FromError(interacterResult);
        }

        var data = await _data.GetData(guildId, ct);

        var hasPermission = CheckPermission(roles, data, interacterId.Value, interacter,
            action switch
            {
                "Ban" => DiscordPermission.BanMembers,
                "Kick" => DiscordPermission.KickMembers,
                "Mute" or "Unmute" => DiscordPermission.ModerateMembers,
                _ => throw new Exception()
            });

        return hasPermission
            ? CheckInteractions(action, guild, roles, targetMember, botMember, interacter)
            : Result<string?>.FromSuccess($"UserCannot{action}Members".Localized());
    }

    private static Result<string?> CheckInteractions(
        string action, IGuild guild, IReadOnlyList<IRole> roles, IGuildMember targetMember, IGuildMember botMember,
        IGuildMember interacter)
    {
        if (!targetMember.User.IsDefined(out var targetUser))
        {
            return new ArgumentNullError(nameof(targetMember.User));
        }

        if (!interacter.User.IsDefined(out var interacterUser))
        {
            return new ArgumentNullError(nameof(interacter.User));
        }

        if (botMember.User == targetMember.User)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Bot".Localized());
        }

        if (targetUser.ID == guild.OwnerID)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Owner".Localized());
        }

        var targetRoles = roles.Where(r => targetMember.Roles.Contains(r.ID)).ToList();
        var botRoles = roles.Where(r => botMember.Roles.Contains(r.ID));

        var targetBotRoleDiff = targetRoles.MaxOrDefault(r => r.Position) - botRoles.MaxOrDefault(r => r.Position);
        if (targetBotRoleDiff >= 0)
        {
            return Result<string?>.FromSuccess($"BotCannot{action}Target".Localized());
        }

        var interacterRoles = roles.Where(r => interacter.Roles.Contains(r.ID));
        var targetInteracterRoleDiff
            = targetRoles.MaxOrDefault(r => r.Position) - interacterRoles.MaxOrDefault(r => r.Position);
        return targetInteracterRoleDiff < 0
            ? Result<string?>.FromSuccess(null)
            : Result<string?>.FromSuccess($"UserCannot{action}Target".Localized());
    }
}
