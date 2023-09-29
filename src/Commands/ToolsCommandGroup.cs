using System.ComponentModel;
using System.Drawing;
using System.Text;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles commands related to tools: /showinfo, /random.
/// </summary>
[UsedImplicitly]
public class ToolsCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public ToolsCommandGroup(
        ICommandContext context, FeedbackService feedback,
        GuildDataService guildData, IDiscordRestGuildAPI guildApi,
        IDiscordRestUserAPI userApi, IDiscordRestChannelAPI channelApi)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that shows information about user.
    /// </summary>
    /// <remarks>
    ///     Information in the output:
    ///     <list type="bullet">
    ///         <item>Display name</item>
    ///         <item>Discord user since</item>
    ///         <item>Guild nickname</item>
    ///         <item>Guild member since</item>
    ///         <item>Nitro booster since</item>
    ///         <item>Guild roles</item>
    ///         <item>Active mute information</item>
    ///         <item>Active ban information</item>
    ///         <item>Is on guild status</item>
    ///     </list>
    /// </remarks>
    /// <param name="target">The user to show info about.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("showinfo")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows info about user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteShowInfoAsync(
        [Description("User to show info about")]
        IUser? target = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ShowUserInfoAsync(target ?? user, currentUser, data, guildId, CancellationToken);
    }

    private async Task<Result> ShowUserInfoAsync(
        IUser user, IUser currentUser, GuildData data, Snowflake guildId, CancellationToken ct = default)
    {
        var builder = new StringBuilder().AppendLine($"### <@{user.ID}>");

        if (user.GlobalName is not null)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoDisplayName)
                .AppendLine(Markdown.InlineCode(user.GlobalName));
        }

        builder.Append("- ").AppendLine(Messages.ShowInfoDiscordUserSince)
            .AppendLine(Markdown.Timestamp(user.ID.Timestamp));

        var memberData = data.GetOrCreateMemberData(user.ID);

        var embedColor = ColorsList.Cyan;

        var guildMemberResult = await _guildApi.GetGuildMemberAsync(guildId, user.ID, ct);
        DateTimeOffset? communicationDisabledUntil = null;
        if (guildMemberResult.IsDefined(out var guildMember))
        {
            communicationDisabledUntil = guildMember.CommunicationDisabledUntil.OrDefault(null);

            embedColor = AppendGuildInformation(embedColor, guildMember, builder);
        }

        var isMuted = (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil) ||
                      communicationDisabledUntil is not null;

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId, user.ID, ct);

        if (isMuted || existingBanResult.IsDefined())
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.ShowInfoPunishments));
        }

        if (isMuted)
        {
            AppendMuteInformation(memberData, communicationDisabledUntil, builder);

            embedColor = ColorsList.Red;
        }

        if (existingBanResult.IsDefined())
        {
            AppendBanInformation(memberData, builder);

            embedColor = ColorsList.Black;
        }

        if (!guildMemberResult.IsSuccess && !existingBanResult.IsDefined())
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.ShowInfoNotOnGuild));

            embedColor = ColorsList.Default;
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ShowInfoTitle, user.GetTag()), currentUser)
            .WithDescription(builder.ToString())
            .WithColour(embedColor)
            .WithLargeAvatar(user)
            .WithFooter($"ID: {user.ID.ToString()}")
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    private static Color AppendGuildInformation(Color color, IGuildMember guildMember, StringBuilder builder)
    {
        if (guildMember.Nickname.IsDefined(out var nickname))
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoGuildNickname)
                .AppendLine(Markdown.InlineCode(nickname));
        }

        builder.Append("- ").AppendLine(Messages.ShowInfoGuildMemberSince)
            .AppendLine(Markdown.Timestamp(guildMember.JoinedAt));

        if (guildMember.PremiumSince.IsDefined(out var premiumSince))
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoGuildMemberPremiumSince)
                .AppendLine(Markdown.Timestamp(premiumSince.Value));
            color = ColorsList.Magenta;
        }

        if (guildMember.Roles.Count > 0)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoGuildRoles);
            for (var i = 0; i < guildMember.Roles.Count - 1; i++)
            {
                builder.Append($"<@&{guildMember.Roles[i]}>, ");
            }

            builder.AppendLine($"<@&{guildMember.Roles[^1]}>");
        }

        return color;
    }

    private static void AppendBanInformation(MemberData memberData, StringBuilder builder)
    {
        if (memberData.BannedUntil < DateTimeOffset.MaxValue)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoBanned)
                .Append(" - ").AppendLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(memberData.BannedUntil.Value)));
            return;
        }

        builder.Append("- ").AppendLine(Messages.ShowInfoBannedPermanently);
    }

    private static void AppendMuteInformation(
        MemberData memberData, DateTimeOffset? communicationDisabledUntil, StringBuilder builder)
    {
        builder.Append("- ").AppendLine(Messages.ShowInfoMuted);
        if (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil)
        {
            builder.Append(" - ").AppendLine(Messages.ShowInfoMutedByMuteRole)
                .Append(" - ").AppendLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(memberData.MutedUntil.Value)));
        }

        if (communicationDisabledUntil is not null)
        {
            builder.Append(" - ").AppendLine(Messages.ShowInfoMutedByTimeout)
                .Append(" - ").AppendLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(communicationDisabledUntil.Value)));
        }
    }

    /// <summary>
    ///     A slash command that generates a random number.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("random")]
    [DiscordDefaultDMPermission(false)]
    [Description("Generates a random number")]
    [UsedImplicitly]
    public async Task<Result> ExecuteRandomAsync(
        [Description("Maximum number")] int max,
        [Description("Minumum number (Default: 1)")]
        int min = 1)
    {
        if (!_context.TryGetContextIDs(out _, out _, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        return await SendRandomAsync(max, min, user, currentUser, CancellationToken);
    }

    private async Task<Result> SendRandomAsync(int max, int min, IUser user, IUser currentUser, CancellationToken ct)
    {
        if (min > max)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(
                    Messages.RandomMinGreaterThanMax, currentUser)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var i = Random.Shared.Next(min, max + 1);

        var embed = new EmbedBuilder().WithSmallTitle(Messages.RandomOutput, user)
            .WithDescription($"# {i}\n({min}-{max})")
            .WithColour(ColorsList.Blue)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
