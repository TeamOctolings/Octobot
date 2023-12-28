using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Octobot.Services.Profiler;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Commands;

/// <summary>
///     Handles the command to kick members of a guild: /kick.
/// </summary>
[UsedImplicitly]
public class KickCommandGroup : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly Profiler _profiler;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public KickCommandGroup(
        ICommandContext context, IDiscordRestChannelAPI channelApi, GuildDataService guildData,
        IFeedbackService feedback, IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi,
        Utility utility, Profiler profiler)
    {
        _context = context;
        _channelApi = channelApi;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
        _profiler = profiler;
    }

    /// <summary>
    ///     A slash command that kicks a Discord member with the specified reason.
    /// </summary>
    /// <param name="target">The member to kick.</param>
    /// <param name="reason">
    ///     The reason for this kick. Must be encoded with <see cref="StringExtensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.RemoveGuildMemberAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the member
    ///     was kicked and vice-versa.
    /// </returns>
    [Command("kick", "кик")]
    [DiscordDefaultMemberPermissions(DiscordPermission.KickMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.KickMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.KickMembers)]
    [Description("Kick member")]
    [UsedImplicitly]
    public async Task<Result> ExecuteKick(
        [Description("Member to kick")] IUser target,
        [Description("Kick reason")] [MaxLength(256)]
        string reason)
    {
        _profiler.Push("kick_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Push("current_user_get");
        // The bot's avatar is used when sending error messages
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return _profiler.PopWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return _profiler.PopWithResult(Result.FromError(executorResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_get");
        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return _profiler.PopWithResult(Result.FromError(guildResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        _profiler.Pop();
        _profiler.Push("target_get");
        var memberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, CancellationToken);

        _profiler.Pop();
        if (!memberResult.IsSuccess)
        {
            _profiler.Push("not_found_send");
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, bot)
                .WithColour(ColorsList.Red).Build();

            return _profiler.ReportWithResult(
                await _feedback.SendContextualEmbedResultAsync(embed, ct: CancellationToken));
        }

        _profiler.Pop();
        return _profiler.ReportWithResult(await KickUserAsync(executor, target, reason, guild, channelId, data, bot,
            CancellationToken));
    }

    private async Task<Result> KickUserAsync(
        IUser executor, IUser target, string reason, IGuild guild, Snowflake channelId, GuildData data, IUser bot,
        CancellationToken ct = default)
    {
        _profiler.Push("interactions_check");
        var interactionResult
            = await _utility.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Kick", ct);
        if (!interactionResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(interactionResult));
        }

        _profiler.Pop();
        if (interactionResult.Entity is not null)
        {
            _profiler.Push("interaction_failed_send");
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct));
        }

        _profiler.Push("dm_create");
        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            _profiler.Push("dm_embed_send");
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereKicked)
                .WithDescription(
                    MarkdownExtensions.BulletPoint(string.Format(Messages.DescriptionActionReason, reason)))
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            await _channelApi.CreateMessageWithEmbedResultAsync(dmChannel.ID, embedResult: dmEmbed, ct: ct);
            _profiler.Pop();
        }

        _profiler.Pop();
        _profiler.Push("member_remove");
        var kickResult = await _guildApi.RemoveGuildMemberAsync(
            guild.ID, target.ID, $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct);
        if (!kickResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(kickResult.Error));
        }

        var memberData = data.GetOrCreateMemberData(target.ID);
        memberData.Roles.Clear();
        memberData.Kicked = true;

        _profiler.Pop();
        _profiler.Push("embed_send");
        var title = string.Format(Messages.UserKicked, target.GetTag());
        var description = MarkdownExtensions.BulletPoint(string.Format(Messages.DescriptionActionReason, reason));
        _profiler.Push("action_log");
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description, target, ColorsList.Red, ct: ct);
        if (!logResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(logResult.Error));
        }

        _profiler.Pop();
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserKicked, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }
}
