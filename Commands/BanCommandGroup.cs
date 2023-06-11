using System.ComponentModel;
using System.Net;
using Boyfriend.Services;
using Boyfriend.Services.Data;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

/// <summary>
///     Handles commands related to ban management: /ban and unban.
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
    /// <param name="reason">
    ///     The reason for this ban. Must be encoded with <see cref="WebUtility.UrlEncode" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.CreateGuildBanAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the user
    ///     was banned and vice-versa.
    /// </returns>
    /// <seealso cref="UnBanUserAsync" />
    [Command("ban", "бан")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("банит пидора")]
    public async Task<Result> BanUserAsync([Description("Юзер, кого банить")] IUser target, string reason) {
        // Data checks
        if (!_context.TryGetGuildID(out var guildId))
            return Result.FromError(new ArgumentNullError(nameof(guildId)));
        if (!_context.TryGetUserID(out var userId))
            return Result.FromError(new ArgumentNullError(nameof(userId)));
        if (!_context.TryGetChannelID(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(channelId)));

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.Culture;

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

            var banResult = await _guildApi.CreateGuildBanAsync(
                guildId.Value, target.ID, reason: $"({user.GetTag()}) {WebUtility.UrlEncode(reason)}",
                ct: CancellationToken);
            if (!banResult.IsSuccess)
                return Result.FromError(banResult.Error);

            responseEmbed = new EmbedBuilder().WithSmallTitle(
                    string.Format(Messages.UserBanned, target.GetTag()), target)
                .WithColour(ColorsList.Green).Build();

            if ((cfg.PublicFeedbackChannel is not 0 && cfg.PublicFeedbackChannel != channelId.Value)
                || (cfg.PrivateFeedbackChannel is not 0 && cfg.PrivateFeedbackChannel != channelId.Value)) {
                var logEmbed = new EmbedBuilder().WithSmallTitle(
                        string.Format(Messages.UserBanned, target.GetTag()), target)
                    .WithDescription(string.Format(Messages.DescriptionUserBanned, reason))
                    .WithActionFooter(user)
                    .WithCurrentTimestamp()
                    .WithColour(ColorsList.Red)
                    .Build();

                if (!logEmbed.IsDefined(out var logBuilt))
                    return Result.FromError(logEmbed);

                var builtArray = new[] { logBuilt };
                // Not awaiting to reduce response time
                if (cfg.PrivateFeedbackChannel != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        cfg.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                        ct: CancellationToken);
                if (cfg.PublicFeedbackChannel != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        cfg.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
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
    ///     The reason for this unban. Must be encoded with <see cref="WebUtility.UrlEncode" /> when passed to
    ///     <see cref="IDiscordRestGuildAPI.RemoveGuildBanAsync" />.
    /// </param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that the user
    ///     was unbanned and vice-versa.
    /// </returns>
    /// <seealso cref="BanUserAsync" />
    [Command("unban")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("разбанит пидора")]
    public async Task<Result> UnBanUserAsync([Description("Юзер, кого разбанить")] IUser target, string reason) {
        // Data checks
        if (!_context.TryGetGuildID(out var guildId))
            return Result.FromError(new ArgumentNullError(nameof(guildId)));
        if (!_context.TryGetUserID(out var userId))
            return Result.FromError(new ArgumentNullError(nameof(userId)));
        if (!_context.TryGetChannelID(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(channelId)));

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.Culture;

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
            guildId.Value, target.ID, reason: $"({user.GetTag()}) {WebUtility.UrlEncode(reason)}",
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
                .WithActionFooter(user)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Green)
                .Build();

            if (!logEmbed.IsDefined(out var logBuilt))
                return Result.FromError(logEmbed);

            var builtArray = new[] { logBuilt };

            // Not awaiting to reduce response time
            if (cfg.PrivateFeedbackChannel != channelId.Value)
                _ = _channelApi.CreateMessageAsync(
                    cfg.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                    ct: CancellationToken);
            if (cfg.PublicFeedbackChannel != channelId.Value)
                _ = _channelApi.CreateMessageAsync(
                    cfg.PublicFeedbackChannel.ToDiscordSnowflake(), embeds: builtArray,
                    ct: CancellationToken);
        }


        if (!responseEmbed.IsDefined(out var built))
            return Result.FromError(responseEmbed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
