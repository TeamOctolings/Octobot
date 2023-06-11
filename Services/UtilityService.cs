using System.Text;
using Boyfriend.Data;
using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Services;

/// <summary>
///     Provides utility methods that cannot be transformed to extension methods because they require usage
///     of some Discord APIs.
/// </summary>
public class UtilityService : IHostedService {
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;
    private readonly IDiscordRestGuildAPI               _guildApi;
    private readonly IDiscordRestUserAPI                _userApi;

    public UtilityService(
        IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi, IDiscordRestGuildScheduledEventAPI eventApi) {
        _guildApi = guildApi;
        _userApi = userApi;
        _eventApi = eventApi;
    }

    public Task StartAsync(CancellationToken ct) {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) {
        return Task.CompletedTask;
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
        Snowflake guildId, Snowflake interacterId, Snowflake targetId, string action, CancellationToken ct = default) {
        if (interacterId == targetId)
            return Result<string?>.FromSuccess($"UserCannot{action}Themselves".Localized());

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result<string?>.FromError(currentUserResult);
        if (currentUser.ID == targetId)
            return Result<string?>.FromSuccess($"UserCannot{action}Bot".Localized());

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: ct);
        if (!guildResult.IsDefined(out var guild))
            return Result<string?>.FromError(guildResult);
        if (targetId == guild.OwnerID) return Result<string?>.FromSuccess($"UserCannot{action}Owner".Localized());

        var targetMemberResult = await _guildApi.GetGuildMemberAsync(guildId, targetId, ct);
        if (!targetMemberResult.IsDefined(out var targetMember))
            return Result<string?>.FromSuccess(null);

        var currentMemberResult = await _guildApi.GetGuildMemberAsync(guildId, currentUser.ID, ct);
        if (!currentMemberResult.IsDefined(out var currentMember))
            return Result<string?>.FromError(currentMemberResult);

        var rolesResult = await _guildApi.GetGuildRolesAsync(guildId, ct);
        if (!rolesResult.IsDefined(out var roles))
            return Result<string?>.FromError(rolesResult);

        var targetRoles = roles.Where(r => targetMember.Roles.Contains(r.ID)).ToList();
        var botRoles = roles.Where(r => currentMember.Roles.Contains(r.ID));

        var targetBotRoleDiff = targetRoles.MaxOrDefault(r => r.Position) - botRoles.MaxOrDefault(r => r.Position);
        if (targetBotRoleDiff >= 0)
            return Result<string?>.FromSuccess($"BotCannot{action}Target".Localized());

        if (interacterId == guild.OwnerID)
            return Result<string?>.FromSuccess(null);

        var interacterResult = await _guildApi.GetGuildMemberAsync(guildId, interacterId, ct);
        if (!interacterResult.IsDefined(out var interacter))
            return Result<string?>.FromError(interacterResult);

        var interacterRoles = roles.Where(r => interacter.Roles.Contains(r.ID));
        var targetInteracterRoleDiff
            = targetRoles.MaxOrDefault(r => r.Position) - interacterRoles.MaxOrDefault(r => r.Position);
        if (targetInteracterRoleDiff >= 0)
            return Result<string?>.FromSuccess($"UserCannot{action}Target".Localized());

        return Result<string?>.FromSuccess(null);
    }

    public async Task<Result<string>> GetEventNotificationMentions(
        GuildData data, IGuildScheduledEvent scheduledEvent, CancellationToken ct = default) {
        var builder = new StringBuilder();
        var receivers = data.Configuration.EventStartedReceivers;
        var role = data.Configuration.EventNotificationRole.ToDiscordSnowflake();
        var usersResult = await _eventApi.GetGuildScheduledEventUsersAsync(
            scheduledEvent.GuildID, scheduledEvent.ID, withMember: true, ct: ct);
        if (!usersResult.IsDefined(out var users)) return Result<string>.FromError(usersResult);

        if (receivers.Contains(GuildConfiguration.NotificationReceiver.Role) && role.Value is not 0)
            builder.Append($"{Mention.Role(role)} ");
        if (receivers.Contains(GuildConfiguration.NotificationReceiver.Interested))
            builder = users.Where(
                    user => {
                        if (!user.GuildMember.IsDefined(out var member)) return true;
                        return !member.Roles.Contains(role);
                    })
                .Aggregate(builder, (current, user) => current.Append($"{Mention.User(user.User)} "));
        return builder.ToString();
    }
}
