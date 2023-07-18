using System.ComponentModel;
using System.Text;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
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

namespace Boyfriend.Commands;

/// <summary>
///     Handles commands related to ban management: /ban and /unban.
/// </summary>
[UsedImplicitly]
public class BanCommandGroup : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestGuildAPI   _guildApi;
    private readonly IDiscordRestUserAPI    _userApi;
    private readonly UtilityService         _utility;

    public BanCommandGroup(
        ICommandContext context,         IDiscordRestChannelAPI channelApi, GuildDataService    dataService,
        FeedbackService feedbackService, IDiscordRestGuildAPI   guildApi,   IDiscordRestUserAPI userApi,
        UtilityService  utility) {
        _context = context;
        _channelApi = channelApi;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that bans a Discord user with the specified reason.
    /// </summary>
    /// <param name="target">The user to ban.</param>
    /// <param name="duration">The duration for this ban. The user will be automatically unbanned after this duration.</param>
    /// <param name="reason">
    ///     The reason for this ban. Must be encoded with <see cref="Extensions.EncodeHeader" /> when passed to
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
    public async Task<Result> ExecuteBan(
        [Description("User to ban")]  IUser     target,
        [Description("Ban reason")]   string    reason,
        [Description("Ban duration")] TimeSpan? duration = null) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));
        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);
        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);
        var guildResult = await _guildApi.GetGuildAsync(guildId.Value, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
            return Result.FromError(guildResult);

        return await BanUserAsync(target, reason, duration, guild, channelId.Value, user, currentUser);
    }

    private async Task<Result> BanUserAsync(
        IUser target, string reason, TimeSpan? duration, IGuild guild, Snowflake channelId,
        IUser user,   IUser  currentUser) {
        var data = await _dataService.GetData(guild.ID, CancellationToken);
        var cfg = data.Settings;
        Messages.Culture = GuildSettings.Language.Get(cfg);

        var existingBanResult = await _guildApi.GetGuildBanAsync(guild.ID, target.ID, CancellationToken);
        if (existingBanResult.IsDefined()) {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserAlreadyBanned, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedbackService.SendContextualEmbedResultAsync(failedEmbed, CancellationToken);
        }

        var interactionResult
            = await _utility.CheckInteractionsAsync(guild.ID, user.ID, target.ID, "Ban", CancellationToken);
        if (!interactionResult.IsSuccess)
            return Result.FromError(interactionResult);

        if (interactionResult.Entity is not null) {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedbackService.SendContextualEmbedResultAsync(errorEmbed, CancellationToken);
        }

        var builder = new StringBuilder().AppendLine(string.Format(Messages.DescriptionActionReason, reason));
        if (duration is not null)
            builder.Append(
                string.Format(
                    Messages.DescriptionActionExpiresAt,
                    Markdown.Timestamp(DateTimeOffset.UtcNow.Add(duration.Value))));
        var title = string.Format(Messages.UserBanned, target.GetTag());
        var description = builder.ToString();

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, CancellationToken);
        if (dmChannelResult.IsDefined(out var dmChannel)) {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereBanned)
                .WithDescription(description)
                .WithActionFooter(user)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            if (!dmEmbed.IsDefined(out var dmBuilt))
                return Result.FromError(dmEmbed);
            await _channelApi.CreateMessageAsync(dmChannel.ID, embeds: new[] { dmBuilt }, ct: CancellationToken);
        }

        var banResult = await _guildApi.CreateGuildBanAsync(
            guild.ID, target.ID, reason: $"({user.GetTag()}) {reason}".EncodeHeader(),
            ct: CancellationToken);
        if (!banResult.IsSuccess)
            return Result.FromError(banResult.Error);
        var memberData = data.GetMemberData(target.ID);
        memberData.BannedUntil
            = duration is not null ? DateTimeOffset.UtcNow.Add(duration.Value) : DateTimeOffset.MaxValue;
        memberData.Roles.Clear();

        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        _utility.LogActionAsync(cfg, channelId, title, target, description, user, CancellationToken);

        return await _feedbackService.SendContextualEmbedResultAsync(embed, CancellationToken);
    }

    /// <summary>
    ///     A slash command that unbans a Discord user with the specified reason.
    /// </summary>
    /// <param name="target">The user to unban.</param>
    /// <param name="reason">
    ///     The reason for this unban. Must be encoded with <see cref="Extensions.EncodeHeader" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.RemoveGuildBanAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the user
    ///     was unbanned and vice-versa.
    /// </returns>
    /// <seealso cref="ExecuteBan" />
    /// <seealso cref="GuildUpdateService.TickGuildAsync"/>
    [Command("unban")]
    [DiscordDefaultMemberPermissions(DiscordPermission.BanMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Unban user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnban(
        [Description("User to unban")] IUser  target,
        [Description("Unban reason")]  string reason) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));
        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);
        // Needed to get the tag and avatar
        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        return await UnbanUserAsync(target, reason, guildId.Value, channelId.Value, user, currentUser);
    }

    private async Task<Result> UnbanUserAsync(
        IUser target, string reason, Snowflake guildId, Snowflake channelId, IUser user, IUser currentUser) {
        var cfg = await _dataService.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId, target.ID, CancellationToken);
        if (!existingBanResult.IsDefined()) {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserNotBanned, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedbackService.SendContextualEmbedResultAsync(errorEmbed, CancellationToken);
        }

        var unbanResult = await _guildApi.RemoveGuildBanAsync(
            guildId, target.ID, $"({user.GetTag()}) {reason}".EncodeHeader(),
            ct: CancellationToken);
        if (!unbanResult.IsSuccess)
            return Result.FromError(unbanResult.Error);

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserUnbanned, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        var title = string.Format(Messages.UserUnbanned, target.GetTag());
        var description = string.Format(Messages.DescriptionActionReason, reason);
        var logResult = _utility.LogActionAsync(cfg, channelId, title, target, description, user, CancellationToken);
        if (!logResult.IsSuccess)
            return Result.FromError(logResult.Error);

        return await _feedbackService.SendContextualEmbedResultAsync(embed, CancellationToken);
    }
}
