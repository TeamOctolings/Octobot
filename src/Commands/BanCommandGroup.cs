using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Parsers;
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
///     Handles commands related to ban management: /ban and /unban.
/// </summary>
[UsedImplicitly]
public class BanCommandGroup : CommandGroup
{
    private readonly AccessControlService _access;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public BanCommandGroup(AccessControlService access, IDiscordRestChannelAPI channelApi, ICommandContext context,
        IFeedbackService feedback, IDiscordRestGuildAPI guildApi, GuildDataService guildData,
        IDiscordRestUserAPI userApi, Utility utility)
    {
        _access = access;
        _channelApi = channelApi;
        _context = context;
        _feedback = feedback;
        _guildApi = guildApi;
        _guildData = guildData;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that bans a Discord user with the specified reason.
    /// </summary>
    /// <param name="target">The user to ban.</param>
    /// <param name="duration">The duration for this ban. The user will be automatically unbanned after this duration.</param>
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
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageMessages)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Ban user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteBanAsync(
        [Description("User to ban")] IUser target,
        [Description("Ban reason")] [MaxLength(256)]
        string reason,
        [Description("Ban duration (e.g. 1h30m)")]
        string? duration = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The bot's avatar is used when sending error messages
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return ResultExtensions.FromError(guildResult);
        }

        var data = await _guildData.GetData(guild.ID, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        if (duration is null)
        {
            return await BanUserAsync(executor, target, reason, null, guild, data, channelId, bot,
                CancellationToken);
        }

        var parseResult = TimeSpanParser.TryParse(duration);
        if (!parseResult.IsDefined(out var timeSpan))
        {
            var failedEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.InvalidTimeSpan, bot)
                .WithDescription(Messages.TimeSpanExample)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: CancellationToken);
        }

        return await BanUserAsync(executor, target, reason, timeSpan, guild, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> BanUserAsync(
        IUser executor, IUser target, string reason, TimeSpan? duration, IGuild guild, GuildData data,
        Snowflake channelId,
        IUser bot, CancellationToken ct = default)
    {
        var existingBanResult = await _guildApi.GetGuildBanAsync(guild.ID, target.ID, ct);
        if (existingBanResult.IsDefined())
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserAlreadyBanned, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var interactionResult
            = await _access.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Ban", ct);
        if (!interactionResult.IsSuccess)
        {
            return ResultExtensions.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: ct);
        }

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

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereBanned)
                .WithDescription(description)
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            await _channelApi.CreateMessageWithEmbedResultAsync(dmChannel.ID, embedResult: dmEmbed, ct: ct);
        }

        var memberData = data.GetOrCreateMemberData(target.ID);
        memberData.BannedUntil
            = duration is not null ? DateTimeOffset.UtcNow.Add(duration.Value) : DateTimeOffset.MaxValue;

        var banResult = await _guildApi.CreateGuildBanAsync(
            guild.ID, target.ID, reason: $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct: ct);
        if (!banResult.IsSuccess)
        {
            memberData.BannedUntil = null;
            return ResultExtensions.FromError(banResult);
        }

        memberData.Roles.Clear();

        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        _utility.LogAction(
            data.Settings, channelId, executor, title, description, target, ColorsList.Red, ct: ct);

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
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
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageMessages)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Unban user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnban(
        [Description("User to unban")] IUser target,
        [Description("Unban reason")] [MaxLength(256)]
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
            return ResultExtensions.FromError(botResult);
        }

        // Needed to get the tag and avatar
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await UnbanUserAsync(executor, target, reason, guildId, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> UnbanUserAsync(
        IUser executor, IUser target, string reason, Snowflake guildId, GuildData data, Snowflake channelId,
        IUser bot, CancellationToken ct = default)
    {
        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId, target.ID, ct);
        if (!existingBanResult.IsDefined())
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserNotBanned, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: ct);
        }

        var unbanResult = await _guildApi.RemoveGuildBanAsync(
            guildId, target.ID, $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct);
        if (!unbanResult.IsSuccess)
        {
            return ResultExtensions.FromError(unbanResult);
        }

        data.GetOrCreateMemberData(target.ID).BannedUntil = null;

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserUnbanned, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        var title = string.Format(Messages.UserUnbanned, target.GetTag());
        var description =
            new StringBuilder().AppendBulletPoint(string.Format(Messages.DescriptionActionReason, reason));

        _utility.LogAction(
            data.Settings, channelId, executor, title, description.ToString(), target, ColorsList.Green, ct: ct);

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
