using System.ComponentModel;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Services;
using Octobot.Services.Update;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Commands;

/// <summary>
///     Handles commands related to mute management: /mute and /unmute.
/// </summary>
[UsedImplicitly]
public class MuteCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public MuteCommandGroup(
        ICommandContext context, GuildDataService guildData, FeedbackService feedback,
        IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi, UtilityService utility)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that mutes a Discord member with the specified reason.
    /// </summary>
    /// <param name="target">The member to mute.</param>
    /// <param name="duration">The duration for this mute. The member will be automatically unmuted after this duration.</param>
    /// <param name="reason">
    ///     The reason for this mute. Must be encoded with <see cref="Extensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.ModifyGuildMemberAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the member
    ///     was muted and vice-versa.
    /// </returns>
    /// <seealso cref="ExecuteUnmute" />
    [Command("mute", "мут")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ModerateMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ModerateMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.ModerateMembers)]
    [Description("Mute member")]
    [UsedImplicitly]
    public async Task<Result> ExecuteMute(
        [Description("Member to mute")] IUser target,
        [Description("Mute reason")] string reason,
        [Description("Mute duration")] TimeSpan duration)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, CancellationToken);
        if (!memberResult.IsSuccess)
        {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(embed, CancellationToken);
        }

        return await MuteUserAsync(
            target, reason, duration, guildId, data, channelId, user, currentUser, CancellationToken);
    }

    private async Task<Result> MuteUserAsync(
        IUser target, string reason, TimeSpan duration, Snowflake guildId, GuildData data, Snowflake channelId,
        IUser user, IUser currentUser, CancellationToken ct = default)
    {
        var interactionResult
            = await _utility.CheckInteractionsAsync(
                guildId, user.ID, target.ID, "Mute", ct);
        if (!interactionResult.IsSuccess)
        {
            return Result.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var until = DateTimeOffset.UtcNow.Add(duration); // >:)

        var muteMethodResult = await SelectMuteMethodAsync(
            target, reason, duration, guildId, data, user, currentUser, until, ct);
        if (!muteMethodResult.IsSuccess)
        {
            return Result.FromError(muteMethodResult.Error);
        }

        var title = string.Format(Messages.UserMuted, target.GetTag());
        var description = new StringBuilder().Append("- ").AppendLine(string.Format(Messages.DescriptionActionReason, reason))
            .Append("- ").Append(string.Format(
                Messages.DescriptionActionExpiresAt, Markdown.Timestamp(until))).ToString();

        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, user, title, description, target, ColorsList.Red, ct: ct);
        if (!logResult.IsSuccess)
        {
            return Result.FromError(logResult.Error);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserMuted, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    private async Task<Result> SelectMuteMethodAsync(
        IUser target, string reason, TimeSpan duration, Snowflake guildId, GuildData data,
        IUser user, IUser currentUser, DateTimeOffset until, CancellationToken ct)
    {
        var muteRole = GuildSettings.MuteRole.Get(data.Settings);

        if (muteRole.Empty())
        {
            var timeoutResult = await TimeoutUserAsync(
                target, reason, duration, guildId, user, currentUser, until, ct);
            return timeoutResult;
        }

        var muteRoleResult = await RoleMuteUserAsync(
            target, reason, guildId, data, user, until, muteRole, ct);
        return muteRoleResult;
    }

    private async Task<Result> RoleMuteUserAsync(
        IUser target, string reason, Snowflake guildId, GuildData data,
        IUser user, DateTimeOffset until, Snowflake muteRole, CancellationToken ct)
    {
        var assignRoles = new List<Snowflake> { muteRole };
        var memberData = data.GetOrCreateMemberData(target.ID);
        if (!GuildSettings.RemoveRolesOnMute.Get(data.Settings))
        {
            assignRoles.AddRange(memberData.Roles.ConvertAll(r => r.ToSnowflake()));
        }

        var muteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, roles: assignRoles,
            reason: $"({user.GetTag()}) {reason}".EncodeHeader(), ct: ct);
        if (muteResult.IsSuccess)
        {
            memberData.MutedUntil = until;
        }

        return muteResult;
    }

    private async Task<Result> TimeoutUserAsync(
        IUser target, string reason, TimeSpan duration, Snowflake guildId,
        IUser user, IUser currentUser, DateTimeOffset until, CancellationToken ct)
    {
        if (duration.TotalDays >= 28)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.BotCannotMuteTarget, currentUser)
                .WithDescription(Messages.DurationRequiredForTimeOuts)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var muteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, reason: $"({user.GetTag()}) {reason}".EncodeHeader(),
            communicationDisabledUntil: until, ct: ct);
        return muteResult;
    }

    /// <summary>
    ///     A slash command that unmutes a Discord member with the specified reason.
    /// </summary>
    /// <param name="target">The member to unmute.</param>
    /// <param name="reason">
    ///     The reason for this unmute. Must be encoded with <see cref="Extensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.ModifyGuildMemberAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the member
    ///     was unmuted and vice-versa.
    /// </returns>
    /// <seealso cref="ExecuteMute" />
    /// <seealso cref="MemberUpdateService.TickMemberDataAsync" />
    [Command("unmute", "размут")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ModerateMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ModerateMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.ModerateMembers)]
    [Description("Unmute member")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnmute(
        [Description("Member to unmute")] IUser target,
        [Description("Unmute reason")] string reason)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        // Needed to get the tag and avatar
        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, CancellationToken);
        if (!memberResult.IsSuccess)
        {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(embed, CancellationToken);
        }

        return await RemoveMuteAsync(
            target, reason, guildId, data, channelId, user, currentUser, CancellationToken);
    }

    private async Task<Result> RemoveMuteAsync(
        IUser target, string reason, Snowflake guildId, GuildData data, Snowflake channelId, IUser user,
        IUser currentUser, CancellationToken ct = default)
    {
        var interactionResult
            = await _utility.CheckInteractionsAsync(
                guildId, user.ID, target.ID, "Unmute", ct);
        if (!interactionResult.IsSuccess)
        {
            return Result.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var guildMemberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, ct);
        DateTimeOffset? communicationDisabledUntil = null;
        if (guildMemberResult.IsDefined(out var guildMember))
        {
            communicationDisabledUntil = guildMember.CommunicationDisabledUntil.OrDefault(null);
        }

        var memberData = data.GetOrCreateMemberData(target.ID);
        var isMuted = memberData.MutedUntil is not null || communicationDisabledUntil is not null;

        if (!isMuted)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserNotMuted, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var removeMuteRoleAsync =
            await RemoveMuteRoleAsync(target, reason, guildId, memberData, user, CancellationToken);
        if (!removeMuteRoleAsync.IsSuccess)
        {
            return Result.FromError(removeMuteRoleAsync.Error);
        }

        var removeTimeoutResult =
            await RemoveTimeoutAsync(target, reason, guildId, communicationDisabledUntil, user, CancellationToken);
        if (!removeTimeoutResult.IsSuccess)
        {
            return Result.FromError(removeTimeoutResult.Error);
        }

        var title = string.Format(Messages.UserUnmuted, target.GetTag());
        var description = $"- {string.Format(Messages.DescriptionActionReason, reason)}";
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, user, title, description, target, ColorsList.Green, ct: ct);
        if (!logResult.IsSuccess)
        {
            return Result.FromError(logResult.Error);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserUnmuted, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    private async Task<Result> RemoveMuteRoleAsync(
        IUser target, string reason, Snowflake guildId, MemberData memberData, IUser user, CancellationToken ct = default)
    {
        if (memberData.MutedUntil is null)
        {
            return Result.FromSuccess();
        }

        var unmuteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, roles: memberData.Roles.ConvertAll(r => r.ToSnowflake()),
            reason: $"({user.GetTag()}) {reason}".EncodeHeader(), ct: ct);
        if (unmuteResult.IsSuccess)
        {
            memberData.MutedUntil = null;
        }

        return unmuteResult;
    }

    private async Task<Result> RemoveTimeoutAsync(
        IUser target, string reason, Snowflake guildId, DateTimeOffset? communicationDisabledUntil,
        IUser user, CancellationToken ct = default)
    {
        if (communicationDisabledUntil is null)
        {
            return Result.FromSuccess();
        }

        var unmuteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, reason: $"({user.GetTag()}) {reason}".EncodeHeader(),
            communicationDisabledUntil: null, ct: ct);
        return unmuteResult;
    }
}
