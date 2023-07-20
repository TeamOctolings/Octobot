using System.ComponentModel;
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
using Remora.Rest.Core;
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
    public async Task<Result> ExecuteKick(
        [Description("Member to kick")] IUser  target,
        [Description("Kick reason")]    string reason) {
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

        var data = await _dataService.GetData(guildId.Value, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        var memberResult = await _guildApi.GetGuildMemberAsync(guildId.Value, target.ID, CancellationToken);
        if (!memberResult.IsSuccess) {
            var embed = new EmbedBuilder().WithSmallTitle(Messages.UserNotFoundShort, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedbackService.SendContextualEmbedResultAsync(embed, CancellationToken);
        }

        return await KickUserAsync(target, reason, guild, channelId.Value, data, user, currentUser, CancellationToken);
    }

    private async Task<Result> KickUserAsync(
        IUser target, string reason, IGuild guild, Snowflake channelId, GuildData data, IUser user, IUser currentUser,
        CancellationToken ct = default) {
        var interactionResult
            = await _utility.CheckInteractionsAsync(guild.ID, user.ID, target.ID, "Kick", ct);
        if (!interactionResult.IsSuccess)
            return Result.FromError(interactionResult);

        if (interactionResult.Entity is not null) {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedbackService.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel)) {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereKicked)
                .WithDescription(string.Format(Messages.DescriptionActionReason, reason))
                .WithActionFooter(user)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            if (!dmEmbed.IsDefined(out var dmBuilt))
                return Result.FromError(dmEmbed);
            await _channelApi.CreateMessageAsync(dmChannel.ID, embeds: new[] { dmBuilt }, ct: ct);
        }

        var kickResult = await _guildApi.RemoveGuildMemberAsync(
            guild.ID, target.ID, $"({user.GetTag()}) {reason}".EncodeHeader(),
            ct);
        if (!kickResult.IsSuccess)
            return Result.FromError(kickResult.Error);
        data.GetMemberData(target.ID).Roles.Clear();

        var title = string.Format(Messages.UserKicked, target.GetTag());
        var description = string.Format(Messages.DescriptionActionReason, reason);
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, user, title, description, target, ct);
        if (!logResult.IsSuccess)
            return Result.FromError(logResult.Error);

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserKicked, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        return await _feedbackService.SendContextualEmbedResultAsync(embed, ct);
    }
}
