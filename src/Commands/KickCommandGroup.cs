using System.ComponentModel;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to kick members of a guild: /kick.
/// </summary>
[UsedImplicitly]
public class KickCommandGroup : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestGuildAPI   _guildApi;
    private readonly IDiscordRestUserAPI    _userApi;
    private readonly UtilityService         _utility;

    public KickCommandGroup(
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
    ///     A slash command that kicks a Discord member with the specified reason.
    /// </summary>
    /// <param name="target">The member to kick.</param>
    /// <param name="reason">
    ///     The reason for this kick. Must be encoded with <see cref="Extensions.EncodeHeader" /> when passed to
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
    public async Task<Result> KickUserAsync(
        [Description("Member to kick")] IUser  target,
        [Description("Kick reason")]    string reason) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        // The current user's avatar is used when sending error messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var data = await _dataService.GetData(guildId.Value, CancellationToken);
        var cfg = data.Settings;
        Messages.Culture = GuildSettings.Language.Get(cfg);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId.Value, target.ID, CancellationToken);
        if (!memberResult.IsSuccess) {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, currentUser)
                .WithColour(ColorsList.Red).Build();

            if (!embed.IsDefined(out var alreadyBuilt))
                return Result.FromError(embed);

            return (Result)await _feedbackService.SendContextualEmbedAsync(alreadyBuilt, ct: CancellationToken);
        }

        var interactionResult
            = await _utility.CheckInteractionsAsync(guildId.Value, userId.Value, target.ID, "Kick", CancellationToken);
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

            var dmChannelResult = await _userApi.CreateDMAsync(target.ID, CancellationToken);
            if (dmChannelResult.IsDefined(out var dmChannel)) {
                var guildResult = await _guildApi.GetGuildAsync(guildId.Value, ct: CancellationToken);
                if (!guildResult.IsDefined(out var guild))
                    return Result.FromError(guildResult);

                var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                    .WithTitle(Messages.YouWereKicked)
                    .WithDescription(string.Format(Messages.DescriptionActionReason, reason))
                    .WithActionFooter(user)
                    .WithCurrentTimestamp()
                    .WithColour(ColorsList.Red)
                    .Build();

                if (!dmEmbed.IsDefined(out var dmBuilt))
                    return Result.FromError(dmEmbed);
                await _channelApi.CreateMessageAsync(dmChannel.ID, embeds: new[] { dmBuilt }, ct: CancellationToken);
            }

            var kickResult = await _guildApi.RemoveGuildMemberAsync(
                guildId.Value, target.ID, $"({user.GetTag()}) {reason}".EncodeHeader(),
                ct: CancellationToken);
            if (!kickResult.IsSuccess)
                return Result.FromError(kickResult.Error);
            data.GetMemberData(target.ID).Roles.Clear();

            responseEmbed = new EmbedBuilder().WithSmallTitle(
                    string.Format(Messages.UserKicked, target.GetTag()), target)
                .WithColour(ColorsList.Green).Build();

            if ((!GuildSettings.PublicFeedbackChannel.Get(cfg).Empty()
                 && GuildSettings.PublicFeedbackChannel.Get(cfg) != channelId.Value)
                || (!GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty()
                    && GuildSettings.PrivateFeedbackChannel.Get(cfg) != channelId.Value)) {
                var logEmbed = new EmbedBuilder().WithSmallTitle(
                        string.Format(Messages.UserKicked, target.GetTag()), target)
                    .WithDescription(string.Format(Messages.DescriptionActionReason, reason))
                    .WithActionFooter(user)
                    .WithCurrentTimestamp()
                    .WithColour(ColorsList.Red)
                    .Build();

                if (!logEmbed.IsDefined(out var logBuilt))
                    return Result.FromError(logEmbed);

                var builtArray = new[] { logBuilt };
                // Not awaiting to reduce response time
                if (GuildSettings.PublicFeedbackChannel.Get(cfg) != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        GuildSettings.PublicFeedbackChannel.Get(cfg), embeds: builtArray,
                        ct: CancellationToken);
                if (GuildSettings.PrivateFeedbackChannel.Get(cfg) != GuildSettings.PublicFeedbackChannel.Get(cfg)
                    && GuildSettings.PrivateFeedbackChannel.Get(cfg) != channelId.Value)
                    _ = _channelApi.CreateMessageAsync(
                        GuildSettings.PrivateFeedbackChannel.Get(cfg), embeds: builtArray,
                        ct: CancellationToken);
            }
        }

        if (!responseEmbed.IsDefined(out var built))
            return Result.FromError(responseEmbed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
