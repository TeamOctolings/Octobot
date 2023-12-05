using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
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
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public KickCommandGroup(
        ICommandContext context, IDiscordRestChannelAPI channelApi, GuildDataService guildData,
        IFeedbackService feedback, IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi,
        UtilityService utility)
    {
        _context = context;
        _channelApi = channelApi;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
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

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result.FromError(guildResult);
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

        return await KickUserAsync(executor, target, reason, guild, channelId, data, bot, CancellationToken);
    }

    private async Task<Result> KickUserAsync(
        IUser executor, IUser target, string reason, IGuild guild, Snowflake channelId, GuildData data, IUser bot,
        CancellationToken ct = default)
    {
        var interactionResult
            = await _utility.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Kick", ct);
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

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouWereKicked)
                .WithDescription(MarkdownExtensions.BulletPoint(string.Format(Messages.DescriptionActionReason, reason)))
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            if (!dmEmbed.IsDefined(out var dmBuilt))
            {
                return Result.FromError(dmEmbed);
            }

            await _channelApi.CreateMessageAsync(dmChannel.ID, embeds: new[] { dmBuilt }, ct: ct);
        }

        var kickResult = await _guildApi.RemoveGuildMemberAsync(
            guild.ID, target.ID, $"({executor.GetTag()}) {reason}".EncodeHeader(),
            ct);
        if (!kickResult.IsSuccess)
        {
            return Result.FromError(kickResult.Error);
        }

        data.GetOrCreateMemberData(target.ID).Roles.Clear();

        var title = string.Format(Messages.UserKicked, target.GetTag());
        var description = MarkdownExtensions.BulletPoint(string.Format(Messages.DescriptionActionReason, reason));
        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, executor, title, description, target, ColorsList.Red, ct: ct);
        if (!logResult.IsSuccess)
        {
            return Result.FromError(logResult.Error);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.UserKicked, target.GetTag()), target)
            .WithColour(ColorsList.Green).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
