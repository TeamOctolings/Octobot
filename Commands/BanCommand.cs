using System.ComponentModel;
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

public class BanCommand : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestGuildAPI   _guildApi;
    private readonly IDiscordRestUserAPI    _userApi;
    private readonly UtilityService         _utility;

    public BanCommand(
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

    [Command("ban")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("банит пидора")]
    public async Task<Result> BanUserAsync([Description("Юзер, кого банить")] IUser target, string reason) {
        if (!_context.TryGetGuildID(out var guildId))
            return Result.FromError(new ArgumentNullError(nameof(guildId)));
        if (!_context.TryGetUserID(out var userId))
            return Result.FromError(new ArgumentNullError(nameof(userId)));
        if (!_context.TryGetChannelID(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(channelId)));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.Culture;

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId.Value, target.ID, CancellationToken);
        if (existingBanResult.IsDefined(out _)) {
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
                guildId.Value, target.ID, reason: $"({user.GetTag()}) {reason}", ct: CancellationToken);
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

    [Command("unban")]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.BanMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.BanMembers)]
    [Description("разбанит пидора")]
    public async Task<Result> UnBanUserAsync([Description("Юзер, кого банить")] IUser target, string reason) {
        if (!_context.TryGetGuildID(out var guildId))
            return Result.FromError(new ArgumentNullError(nameof(guildId)));
        if (!_context.TryGetUserID(out var userId))
            return Result.FromError(new ArgumentNullError(nameof(userId)));
        if (!_context.TryGetChannelID(out var channelId))
            return Result.FromError(new ArgumentNullError(nameof(channelId)));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.Culture;

        //TODO: Проверка на существующий бан.


        Result<Embed> responseEmbed;

        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        var banResult = await _guildApi.CreateGuildBanAsync(
            guildId.Value, target.ID, reason: $"({user.GetTag()}) {reason}", ct: CancellationToken);
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
