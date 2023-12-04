using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
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
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public MuteCommandGroup(
        ICommandContext context, GuildDataService guildData, IFeedbackService feedback,
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
    ///     The reason for this mute. Must be encoded with <see cref="StringExtensions.EncodeHeader" /> when passed to
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
        [Description("Mute reason")] [MaxLength(256)]
        string reason,
        [Description("Mute duration")] TimeSpan duration)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The bot's avatar is used when sending error messages
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, CancellationToken);
        if (!memberResult.IsSuccess)
        {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(embed, CancellationToken);
        }

        return await MuteUserAsync(executor, target, reason, duration, guildId, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> MuteUserAsync(
        IUser executor, IUser target, string reason, TimeSpan duration, Snowflake guildId, GuildData data,
        Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var interactionResult
            = await _utility.CheckInteractionsAsync(
                guildId, executor.ID, target.ID, "Mute", ct);
        if (!interactionResult.IsSuccess)
        {
            return Result.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var until = DateTimeOffset.UtcNow.Add(duration); // >:)

        var muteMethodResult = await SelectMuteMethodAsync(executor, target, reason, duration, guildId, data, bot, until, ct);
        if (!muteMethodResult.IsSuccess)
        {
            return muteMethodResult;
        }

        var title = string.Format(Messages.UserMuted, target.GetTag());
        var description = new StringBuilder().AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason))
            .AppendBulletPoint(string.Format(
                Messages.DescriptionActionExpiresAt, Markdown.Timestamp(until))).ToString();

        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description, target, ColorsList.Red, ct: ct);
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
        IUser executor, IUser target, string reason, TimeSpan duration, Snowflake guildId, GuildData data,
        IUser bot, DateTimeOffset until, CancellationToken ct)
    {
        var muteRole = GuildSettings.MuteRole.Get(data.Settings);

        if (muteRole.Empty())
        {
            var timeoutResult = await TimeoutUserAsync(executor, target, reason, duration, guildId, bot, until, ct);
            return timeoutResult;
        }

        var muteRoleResult = await RoleMuteUserAsync(executor, target, reason, guildId, data, until, muteRole, ct);
        return muteRoleResult;
    }

    private async Task<Result> RoleMuteUserAsync(
        IUser executor, IUser target, string reason, Snowflake guildId, GuildData data,
        DateTimeOffset until, Snowflake muteRole, CancellationToken ct)
    {
        var assignRoles = new List<Snowflake> { muteRole };
        var memberData = data.GetOrCreateMemberData(target.ID);
        if (!GuildSettings.RemoveRolesOnMute.Get(data.Settings))
        {
            assignRoles.AddRange(memberData.Roles.ConvertAll(r => r.ToSnowflake()));
        }

        var muteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, roles: assignRoles,
            reason: $"({executor.GetTag()}) {reason}".EncodeHeader(), ct: ct);
        if (muteResult.IsSuccess)
        {
            memberData.MutedUntil = until;
        }

        return muteResult;
    }

    private async Task<Result> TimeoutUserAsync(
        IUser executor, IUser target, string reason, TimeSpan duration, Snowflake guildId,
        IUser bot, DateTimeOffset until, CancellationToken ct)
    {
        if (duration.TotalDays >= 28)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.BotCannotMuteTarget, bot)
                .WithDescription(Messages.DurationRequiredForTimeOuts)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var muteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, reason: $"({executor.GetTag()}) {reason}".EncodeHeader(),
            communicationDisabledUntil: until, ct: ct);
        return muteResult;
    }

    /// <summary>
    ///     A slash command that unmutes a Discord member with the specified reason.
    /// </summary>
    /// <param name="target">The member to unmute.</param>
    /// <param name="reason">
    ///     The reason for this unmute. Must be encoded with <see cref="StringExtensions.EncodeHeader" /> when passed to
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
        [Description("Unmute reason")] [MaxLength(256)]
        string reason)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The bot's avatar is used when sending error messages
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        // Needed to get the tag and avatar
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, CancellationToken);
        if (!memberResult.IsSuccess)
        {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(embed, CancellationToken);
        }

        return await RemoveMuteAsync(executor, target, reason, guildId, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> RemoveMuteAsync(
        IUser executor, IUser target, string reason, Snowflake guildId, GuildData data, Snowflake channelId,
        IUser bot, CancellationToken ct = default)
    {
        var interactionResult
            = await _utility.CheckInteractionsAsync(
                guildId, executor.ID, target.ID, "Unmute", ct);
        if (!interactionResult.IsSuccess)
        {
            return Result.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
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
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserNotMuted, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var removeMuteRoleAsync =
            await RemoveMuteRoleAsync(executor, target, reason, guildId, memberData, CancellationToken);
        if (!removeMuteRoleAsync.IsSuccess)
        {
            return Result.FromError(removeMuteRoleAsync.Error);
        }

        var removeTimeoutResult =
            await RemoveTimeoutAsync(executor, target, reason, guildId, communicationDisabledUntil, CancellationToken);
        if (!removeTimeoutResult.IsSuccess)
        {
            return Result.FromError(removeTimeoutResult.Error);
        }

        var title = string.Format(Messages.UserUnmuted, target.GetTag());
        var description = MarkdownExtensions.BulletPoint(string.Format(Messages.DescriptionActionReason, reason));
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description, target, ColorsList.Green, ct: ct);
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
        IUser executor, IUser target, string reason, Snowflake guildId, MemberData memberData, CancellationToken ct = default)
    {
        if (memberData.MutedUntil is null)
        {
            return Result.FromSuccess();
        }

        var unmuteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, roles: memberData.Roles.ConvertAll(r => r.ToSnowflake()),
            reason: $"({executor.GetTag()}) {reason}".EncodeHeader(), ct: ct);
        if (unmuteResult.IsSuccess)
        {
            memberData.MutedUntil = null;
        }

        return unmuteResult;
    }

    private async Task<Result> RemoveTimeoutAsync(
        IUser executor, IUser target, string reason, Snowflake guildId, DateTimeOffset? communicationDisabledUntil,
        CancellationToken ct = default)
    {
        if (communicationDisabledUntil is null)
        {
            return Result.FromSuccess();
        }

        var unmuteResult = await _guildApi.ModifyGuildMemberAsync(
            guildId, target.ID, reason: $"({executor.GetTag()}) {reason}".EncodeHeader(),
            communicationDisabledUntil: null, ct: ct);
        return unmuteResult;
    }
}
