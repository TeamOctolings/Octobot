using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Parsers;
using Octobot.Services;
using Octobot.Services.Profiler;
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
///     Handles commands related to ban management: /ban and /unban.
/// </summary>
[UsedImplicitly]
public class BanCommandGroup : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly Profiler _profiler;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public BanCommandGroup(
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
    ///     A slash command that bans a Discord user with the specified reason.
    /// </summary>
    /// <param name="target">The user to ban.</param>
    /// <param name="stringDuration">The duration for this ban. The user will be automatically unbanned after this duration.</param>
    /// <param name="reason">
    ///     The reason for this ban. Must be encoded with <see cref="StringExtensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.CreateGuildBanAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the user
    ///     was banned and vice-versa.
    /// </returns>
    /// <seealso cref="ExecuteUnban" />
    [Command("ban", "бан")]
    [DiscordDefaultMemberPermissions(DiscordPermission.BanMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Ban user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteBanAsync(
        [Description("User to ban")] IUser target,
        [Description("Ban reason")] [MaxLength(256)]
        string reason,
        [Description("Ban duration")] [Option("duration")]
        string? stringDuration = null)
    {
        _profiler.Push("ban_command");
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
            return _profiler.ReportWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return _profiler.ReportWithResult(Result.FromError(executorResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_get");
        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return _profiler.ReportWithResult(Result.FromError(guildResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guild.ID, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        if (stringDuration is null)
        {
            _profiler.Pop();
            return _profiler.ReportWithResult(await BanUserAsync(executor, target, reason, null, guild, data, channelId,
                bot,
                CancellationToken));
        }

        var parseResult = TimeSpanParser.TryParse(stringDuration);
        if (!parseResult.IsDefined(out var duration))
        {
            _profiler.Push("invalid_timespan_send");
            var failedEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.InvalidTimeSpan, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return _profiler.ReportWithResult(
                await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: CancellationToken));
        }

        _profiler.Pop();
        return _profiler.ReportWithResult(await BanUserAsync(executor, target, reason, duration, guild, data, channelId,
            bot, CancellationToken));
    }

    private async Task<Result> BanUserAsync(
        IUser executor, IUser target, string reason, TimeSpan? duration, IGuild guild, GuildData data,
        Snowflake channelId,
        IUser bot, CancellationToken ct = default)
    {
        _profiler.Push("main");
        _profiler.Push("guild_ban_get");
        var existingBanResult = await _guildApi.GetGuildBanAsync(guild.ID, target.ID, ct);
        if (existingBanResult.IsDefined())
        {
            _profiler.Push("already_banned_send");
            var alreadyBannedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserAlreadyBanned, bot)
                .WithColour(ColorsList.Red).Build();

            return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(alreadyBannedEmbed, ct: ct));
        }

        _profiler.Pop();
        _profiler.Push("interactions_check");
        var interactionResult
            = await _utility.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Ban", ct);
        if (!interactionResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(interactionResult));
        }

        _profiler.Pop();
        if (interactionResult.Entity is not null)
        {
            _profiler.Push("interaction_failed_send");
            var interactionFailedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return _profiler.PopWithResult(
                await _feedback.SendContextualEmbedResultAsync(interactionFailedEmbed, ct: ct));
        }

        _profiler.Push("builder_construction");
        var builder =
            new StringBuilder().AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason));
        if (duration is not null)
        {
            builder.AppendBulletPoint(
                string.Format(
                    Messages.DescriptionActionExpiresAt,
                    Markdown.Timestamp(DateTimeOffset.UtcNow.Add(duration.Value))));
        }

        var title = string.Format(Messages.UserBanned, target.GetTag());
        var description = builder.ToString();

        _profiler.Pop();
        _profiler.Push("dm_create");
        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            _profiler.Push("dm_embed_send");
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereBanned)
                .WithDescription(description)
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            await _channelApi.CreateMessageWithEmbedResultAsync(dmChannel.ID, embedResult: dmEmbed, ct: ct);
            _profiler.Pop();
        }

        _profiler.Pop();
        _profiler.Push("ban_create");
        var banResult = await _guildApi.CreateGuildBanAsync(
            guild.ID, target.ID, reason: $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct: ct);
        if (!banResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(banResult.Error));
        }

        var memberData = data.GetOrCreateMemberData(target.ID);
        memberData.BannedUntil
            = duration is not null ? DateTimeOffset.UtcNow.Add(duration.Value) : DateTimeOffset.MaxValue;
        memberData.Roles.Clear();

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        _profiler.Push("action_log");
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description, target, ColorsList.Red, ct: ct);
        if (!logResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(logResult.Error));
        }

        _profiler.Pop();
        return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }

    /// <summary>
    ///     A slash command that unbans a Discord user with the specified reason.
    /// </summary>
    /// <param name="target">The user to unban.</param>
    /// <param name="reason">
    ///     The reason for this unban. Must be encoded with <see cref="StringExtensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.RemoveGuildBanAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the user
    ///     was unbanned and vice-versa.
    /// </returns>
    /// <seealso cref="ExecuteBanAsync" />
    /// <seealso cref="MemberUpdateService.TickMemberDataAsync" />
    [Command("unban")]
    [DiscordDefaultMemberPermissions(DiscordPermission.BanMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Unban user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnban(
        [Description("User to unban")] IUser target,
        [Description("Unban reason")] [MaxLength(256)]
        string reason)
    {
        _profiler.Push("unban_command");
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
            return _profiler.ReportWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        // Needed to get the tag and avatar
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return _profiler.ReportWithResult(Result.FromError(executorResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await UnbanUserAsync(executor, target, reason, guildId, data, channelId, bot,
            CancellationToken));
    }

    private async Task<Result> UnbanUserAsync(
        IUser executor, IUser target, string reason, Snowflake guildId, GuildData data, Snowflake channelId,
        IUser bot, CancellationToken ct = default)
    {
        _profiler.Push("main");
        _profiler.Push("guild_ban_get");
        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId, target.ID, ct);
        if (!existingBanResult.IsDefined())
        {
            _profiler.Push("not_banned_send");
            var notBannedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserNotBanned, bot)
                .WithColour(ColorsList.Red).Build();

            return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(notBannedEmbed, ct: ct));
        }

        _profiler.Pop();
        _profiler.Push("guild_ban_remove");
        var unbanResult = await _guildApi.RemoveGuildBanAsync(
            guildId, target.ID, $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct);
        if (!unbanResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(unbanResult.Error));
        }

        data.GetOrCreateMemberData(target.ID).BannedUntil = null;

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserUnbanned, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        var title = string.Format(Messages.UserUnbanned, target.GetTag());
        var description =
            new StringBuilder().AppendBulletPoint(string.Format(Messages.DescriptionActionReason, reason));

        _profiler.Push("action_log");
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description.ToString(), target, ColorsList.Green, ct: ct);
        if (!logResult.IsSuccess)
        {
            return _profiler.PopWithResult(Result.FromError(logResult.Error));
        }

        _profiler.Pop();
        return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }
}
