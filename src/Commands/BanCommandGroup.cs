using System.ComponentModel;
using System.Text;
using Boyfriend.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

/// <summary>
///     Handles commands related to ban management: /ban and /unban.
/// </summary>
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
    /// <seealso cref="UnbanUserAsync" />
    [Command("ban", "бан")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Ban user")]
    public async Task<Result> BanUserAsync(
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

        var data = await _dataService.GetData(guildId.Value, CancellationToken);
        var cfg = data.Configuration;
        Messages.Culture = data.Culture;

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId.Value, target.ID, CancellationToken);
        if (existingBanResult.IsDefined()) {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserAlreadyBanned, currentUser)
                .WithColour(ColorsList.Red).Build();

            if (!embed.IsDefined(out var alreadyBuilt))
                return Result.FromError(embed);

            return (Result)await _feedbackService.SendContextualEmbedAsync(alreadyBuilt, ct: CancellationToken);
        }

        var interactionResult
            = await _utility.CheckInteractionsAsync(guildId.Value, userId.Value, target.ID, "Ban", CancellationToken);
        if (!interactionResult.IsSuccess)
            return Result.FromError(interactionResult);

        Result<Embed> responseEmbed;
        if (interactionResult.Entity is not null) {
            responseEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, currentUser)
                .WithColour(ColorsList.Red).Build();
        } else {
            var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
            if (!userResult.IsDefined(out var user))
                return Result.FromError(userResult);

            var builder = new StringBuilder().AppendLine(string.Format(Messages.DescriptionActionReason, reason));
            if (duration is not null)
                builder.Append(
                    string.Format(
                        Messages.DescriptionActionExpiresAt,
                        Markdown.Timestamp(DateTimeOffset.UtcNow.Add(duration.Value))));
            var description = builder.ToString();

            var dmChannelResult = await _userApi.CreateDMAsync(target.ID, CancellationToken);
            if (dmChannelResult.IsDefined(out var dmChannel)) {
                var guildResult = await _guildApi.GetGuildAsync(guildId.Value, ct: CancellationToken);
                if (!guildResult.IsDefined(out var guild))
                    return Result.FromError(guildResult);

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
                guildId.Value, target.ID, reason: $"({user.GetTag()}) {reason}".EncodeHeader(),
                ct: CancellationToken);
            if (!banResult.IsSuccess)
                return Result.FromError(banResult.Error);
            var memberData = data.GetMemberData(target.ID);
            memberData.BannedUntil
                = duration is not null ? DateTimeOffset.UtcNow.Add(duration.Value) : DateTimeOffset.MaxValue;
            memberData.Roles.Clear();

            responseEmbed = new EmbedBuilder().WithSmallTitle(
                    string.Format(Messages.UserBanned, target.GetTag()), target)
                .WithColour(ColorsList.Green).Build();

            if ((cfg.PublicFeedbackChannel is not 0 && cfg.PublicFeedbackChannel != channelId.Value)
                || (cfg.PrivateFeedbackChannel is not 0 && cfg.PrivateFeedbackChannel != channelId.Value)) {
                var logEmbed = new EmbedBuilder().WithSmallTitle(
                        string.Format(Messages.UserBanned, target.GetTag()), target)
                    .WithDescription(description)
                    .WithActionFooter(user)
                    .WithCurrentTimestamp()
                    .WithColour(ColorsList.Red)
                    .Build();

                if (!logEmbed.IsDefined(out var logBuilt))
                    return Result.FromError(logEmbed);

                var builtArray = new[] { logBuilt };
                // Not awaiting to reduce response time
                if (cfg.PublicFeedbackChannel != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        cfg.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                        ct: CancellationToken);
                if (cfg.PrivateFeedbackChannel != cfg.PublicFeedbackChannel
                    && cfg.PrivateFeedbackChannel != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        cfg.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                        ct: CancellationToken);
            }
        }

        if (!responseEmbed.IsDefined(out var built))
            return Result.FromError(responseEmbed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
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
    /// <seealso cref="BanUserAsync" />
    /// <seealso cref="GuildUpdateService.TickGuildAsync"/>
    [Command("unban")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("Unban user")]
    public async Task<Result> UnbanUserAsync(
        [Description("User to unban")] IUser  target,
        [Description("Unban reason")]  string reason) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId.Value, target.ID, CancellationToken);
        if (!existingBanResult.IsDefined()) {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotBanned, currentUser)
                .WithColour(ColorsList.Red).Build();

            if (!embed.IsDefined(out var alreadyBuilt))
                return Result.FromError(embed);

            return (Result)await _feedbackService.SendContextualEmbedAsync(alreadyBuilt, ct: CancellationToken);
        }

        // Needed to get the tag and avatar
        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        var unbanResult = await _guildApi.RemoveGuildBanAsync(
            guildId.Value, target.ID, $"({user.GetTag()}) {reason}".EncodeHeader(),
            ct: CancellationToken);
        if (!unbanResult.IsSuccess)
            return Result.FromError(unbanResult.Error);

        var responseEmbed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserUnbanned, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        if ((cfg.PublicFeedbackChannel is not 0 && cfg.PublicFeedbackChannel != channelId.Value)
            || (cfg.PrivateFeedbackChannel is not 0 && cfg.PrivateFeedbackChannel != channelId.Value)) {
            var logEmbed = new EmbedBuilder().WithSmallTitle(
                    string.Format(Messages.UserUnbanned, target.GetTag()), target)
                .WithDescription(string.Format(Messages.DescriptionActionReason, reason))
                .WithActionFooter(user)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Green)
                .Build();

            if (!logEmbed.IsDefined(out var logBuilt))
                return Result.FromError(logEmbed);

            var builtArray = new[] { logBuilt };

            // Not awaiting to reduce response time
            if (cfg.PublicFeedbackChannel != channelId.Value)
                _ = _channelApi.CreateMessageAsync(
                    cfg.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                    ct: CancellationToken);
            if (cfg.PrivateFeedbackChannel != cfg.PublicFeedbackChannel
                && cfg.PrivateFeedbackChannel != channelId.Value)
                _ = _channelApi.CreateMessageAsync(
                    cfg.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                    ct: CancellationToken);
        }

        if (!responseEmbed.IsDefined(out var built))
            return Result.FromError(responseEmbed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
