using System.ComponentModel;
using System.Drawing;
using System.Text;
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
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Extensions;
using TeamOctolings.Octobot.Services;

namespace TeamOctolings.Octobot.Commands;

/// <summary>
///     Handles info commands: /userinfo, /guildinfo.
/// </summary>
[UsedImplicitly]
public sealed class InfoCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public InfoCommandGroup(
        ICommandContext context, IFeedbackService feedback,
        GuildDataService guildData, IDiscordRestGuildAPI guildApi,
        IDiscordRestUserAPI userApi)
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
    [Command("userinfo")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows info about user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUserInfoAsync(
        [Description("User to show info about")]
        IUser? target = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

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

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ShowUserInfoAsync(target ?? executor, bot, data, guildId, CancellationToken);
    }

    private async Task<Result> ShowUserInfoAsync(
        IUser target, IUser bot, GuildData data, Snowflake guildId, CancellationToken ct = default)
    {
        var builder = new StringBuilder().AppendLine($"### <@{target.ID}>");

        if (target.GlobalName.IsDefined(out var globalName))
        {
            builder.AppendBulletPointLine(Messages.UserInfoDisplayName)
                .AppendLine(Markdown.InlineCode(globalName));
        }

        builder.AppendBulletPointLine(Messages.UserInfoDiscordUserSince)
            .AppendLine(Markdown.Timestamp(target.ID.Timestamp));

        var memberData = data.GetOrCreateMemberData(target.ID);

        var embedColor = ColorsList.Cyan;

        var guildMemberResult = await _guildApi.GetGuildMemberAsync(guildId, target.ID, ct);
        DateTimeOffset? communicationDisabledUntil = null;
        if (guildMemberResult.IsDefined(out var guildMember))
        {
            communicationDisabledUntil = guildMember.CommunicationDisabledUntil.OrDefault(null);

            embedColor = AppendGuildInformation(embedColor, guildMember, builder);
        }

        var wasMuted = (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil) ||
                       communicationDisabledUntil is not null;
        var wasBanned = memberData.BannedUntil is not null;
        var wasKicked = memberData.Kicked;

        if (wasMuted || wasBanned || wasKicked)
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.UserInfoPunishments));

            embedColor = AppendPunishmentsInformation(wasMuted, wasKicked, wasBanned, memberData,
                builder, embedColor, communicationDisabledUntil);
        }

        if (!guildMemberResult.IsSuccess && !wasBanned)
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.UserInfoNotOnGuild));

            embedColor = ColorsList.Default;
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.InformationAbout, target.GetTag()), bot)
            .WithDescription(builder.ToString())
            .WithColour(embedColor)
            .WithLargeUserAvatar(target)
            .WithFooter($"ID: {target.ID.ToString()}")
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private static Color AppendPunishmentsInformation(bool wasMuted, bool wasKicked, bool wasBanned,
        MemberData memberData, StringBuilder builder, Color embedColor, DateTimeOffset? communicationDisabledUntil)
    {
        if (wasMuted)
        {
            AppendMuteInformation(memberData, communicationDisabledUntil, builder);
            embedColor = ColorsList.Red;
        }

        if (wasKicked)
        {
            builder.AppendBulletPointLine(Messages.UserInfoKicked);
        }

        if (wasBanned)
        {
            AppendBanInformation(memberData, builder);
            embedColor = ColorsList.Black;
        }

        return embedColor;
    }

    private static Color AppendGuildInformation(Color color, IGuildMember guildMember, StringBuilder builder)
    {
        if (guildMember.Nickname.IsDefined(out var nickname))
        {
            builder.AppendBulletPointLine(Messages.UserInfoGuildNickname)
                .AppendLine(Markdown.InlineCode(nickname));
        }

        builder.AppendBulletPointLine(Messages.UserInfoGuildMemberSince)
            .AppendLine(Markdown.Timestamp(guildMember.JoinedAt));

        if (guildMember.PremiumSince.IsDefined(out var premiumSince))
        {
            builder.AppendBulletPointLine(Messages.UserInfoGuildMemberPremiumSince)
                .AppendLine(Markdown.Timestamp(premiumSince.Value));
            color = ColorsList.Magenta;
        }

        if (guildMember.Roles.Count > 0)
        {
            builder.AppendBulletPointLine(Messages.UserInfoGuildRoles);
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
            builder.AppendBulletPointLine(Messages.UserInfoBanned)
                .AppendSubBulletPointLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(memberData.BannedUntil.Value)));
            return;
        }

        builder.AppendBulletPointLine(Messages.UserInfoBannedPermanently);
    }

    private static void AppendMuteInformation(
        MemberData memberData, DateTimeOffset? communicationDisabledUntil, StringBuilder builder)
    {
        builder.AppendBulletPointLine(Messages.UserInfoMuted);
        if (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil)
        {
            builder.AppendSubBulletPointLine(Messages.UserInfoMutedByMuteRole)
                .AppendSubBulletPointLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(memberData.MutedUntil.Value)));
        }

        if (communicationDisabledUntil is not null)
        {
            builder.AppendSubBulletPointLine(Messages.UserInfoMutedByTimeout)
                .AppendSubBulletPointLine(string.Format(
                    Messages.DescriptionActionExpiresAt, Markdown.Timestamp(communicationDisabledUntil.Value)));
        }
    }

    /// <summary>
    ///     A slash command that shows guild information.
    /// </summary>
    /// <remarks>
    ///     Information in the output:
    ///     <list type="bullet">
    ///         <item>Guild description</item>
    ///         <item>Creation date</item>
    ///         <item>Guild's language</item>
    ///         <item>Guild's owner</item>
    ///         <item>Boost level</item>
    ///         <item>Boost count</item>
    ///     </list>
    /// </remarks>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("guildinfo")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows info about current guild")]
    [UsedImplicitly]
    public async Task<Result> ExecuteGuildInfoAsync()
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return ResultExtensions.FromError(guildResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ShowGuildInfoAsync(bot, guild, CancellationToken);
    }

    private Task<Result> ShowGuildInfoAsync(IUser bot, IGuild guild, CancellationToken ct = default)
    {
        var description = new StringBuilder().AppendLine($"## {guild.Name}");

        if (guild.Description is not null)
        {
            description.AppendBulletPointLine(Messages.GuildInfoDescription)
                .AppendLine(Markdown.InlineCode(guild.Description));
        }

        description.AppendBulletPointLine(Messages.GuildInfoCreatedAt)
            .AppendLine(Markdown.Timestamp(guild.ID.Timestamp))
            .AppendBulletPointLine(Messages.GuildInfoOwner)
            .AppendLine(Mention.User(guild.OwnerID));

        var embedColor = ColorsList.Cyan;

        if (guild.PremiumTier > PremiumTier.None)
        {
            description.Append("### ").AppendLine(Messages.GuildInfoServerBoost)
                .AppendBulletPoint(Messages.GuildInfoBoostTier)
                .Append(": ").AppendLine(Markdown.InlineCode(guild.PremiumTier.ToString()))
                .AppendBulletPoint(Messages.GuildInfoBoostCount)
                .Append(": ").AppendLine(Markdown.InlineCode(guild.PremiumSubscriptionCount.ToString()));
            embedColor = ColorsList.Magenta;
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.InformationAbout, guild.Name), bot)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .WithLargeGuildIcon(guild)
            .WithGuildBanner(guild)
            .WithFooter($"ID: {guild.ID.ToString()}")
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
