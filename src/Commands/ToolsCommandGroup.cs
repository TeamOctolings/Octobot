using System.ComponentModel;
using System.Drawing;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
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

namespace Octobot.Commands;

/// <summary>
///     Handles tool commands: /userinfo, /guildinfo, /random, /timestamp.
/// </summary>
[UsedImplicitly]
public class ToolsCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public ToolsCommandGroup(
        ICommandContext context, IFeedbackService feedback,
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
            return Result.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ShowUserInfoAsync(target ?? executor, bot, data, guildId, CancellationToken);
    }

    private async Task<Result> ShowUserInfoAsync(
        IUser target, IUser bot, GuildData data, Snowflake guildId, CancellationToken ct = default)
    {
        var builder = new StringBuilder().AppendLine($"### <@{target.ID}>");

        if (target.GlobalName is not null)
        {
            builder.AppendBulletPointLine(Messages.UserInfoDisplayName)
                .AppendLine(Markdown.InlineCode(target.GlobalName));
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

        var isMuted = (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil) ||
                      communicationDisabledUntil is not null;
        var wasBanned = memberData.BannedUntil is not null;
        var wasKicked = memberData.Kicked;

        if (isMuted || wasBanned || wasKicked)
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.UserInfoPunishments));

            embedColor = AppendPunishmentsInformation(isMuted, wasKicked, wasBanned, memberData,
                builder, embedColor, communicationDisabledUntil);
        }

        if (!guildMemberResult.IsSuccess || !wasBanned || !wasKicked)
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

    private static Color AppendPunishmentsInformation(bool isMuted, bool wasKicked, bool wasBanned,
        MemberData memberData, StringBuilder builder, Color embedColor, DateTimeOffset? communicationDisabledUntil)
    {
        if (isMuted)
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
    [Description("Shows info current guild")]
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
            return Result.FromError(botResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result.FromError(guildResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ShowGuildInfoAsync(bot, guild, CancellationToken);
    }

    private Task<Result> ShowGuildInfoAsync(IUser bot, IGuild guild, CancellationToken ct)
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

    /// <summary>
    ///     A slash command that generates a random number using maximum and minimum numbers.
    /// </summary>
    /// <param name="first">The first number used for randomization.</param>
    /// <param name="second">The second number used for randomization. Default value: 0</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("random")]
    [DiscordDefaultDMPermission(false)]
    [Description("Generates a random number")]
    [UsedImplicitly]
    public async Task<Result> ExecuteRandomAsync(
        [Description("First number")] long first,
        [Description("Second number (Default: 0)")]
        long? second = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await SendRandomNumberAsync(first, second, executor, CancellationToken);
    }

    private Task<Result> SendRandomNumberAsync(long first, long? secondNullable,
        IUser executor, CancellationToken ct)
    {
        const long secondDefault = 0;
        var second = secondNullable ?? secondDefault;

        var min = Math.Min(first, second);
        var max = Math.Max(first, second);

        var i = Random.Shared.NextInt64(min, max + 1);

        var description = new StringBuilder().Append("# ").Append(i);

        description.AppendLine().AppendBulletPoint(string.Format(
            Messages.RandomMin, Markdown.InlineCode(min.ToString())));
        if (secondNullable is null && first >= secondDefault)
        {
            description.Append(' ').Append(Messages.Default);
        }

        description.AppendLine().AppendBulletPoint(string.Format(
            Messages.RandomMax, Markdown.InlineCode(max.ToString())));
        if (secondNullable is null && first < secondDefault)
        {
            description.Append(' ').Append(Messages.Default);
        }

        var embedColor = ColorsList.Blue;
        if (secondNullable is not null && min == max)
        {
            description.AppendLine().Append(Markdown.Italicise(Messages.RandomMinMaxSame));
            embedColor = ColorsList.Red;
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.RandomTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private static readonly TimestampStyle[] AllStyles =
    [
        TimestampStyle.ShortDate,
        TimestampStyle.LongDate,
        TimestampStyle.ShortTime,
        TimestampStyle.LongTime,
        TimestampStyle.ShortDateTime,
        TimestampStyle.LongDateTime,
        TimestampStyle.RelativeTime
    ];

    /// <summary>
    ///     A slash command that shows the current timestamp with an optional offset in all styles supported by Discord.
    /// </summary>
    /// <param name="offset">The offset for the current timestamp.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("timestamp")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows a timestamp in all styles")]
    [UsedImplicitly]
    public async Task<Result> ExecuteTimestampAsync(
        [Description("Offset from current time")]
        TimeSpan? offset = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await SendTimestampAsync(offset, executor, CancellationToken);
    }

    private Task<Result> SendTimestampAsync(TimeSpan? offset, IUser executor, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.Add(offset ?? TimeSpan.Zero).ToUnixTimeSeconds();

        var description = new StringBuilder().Append("# ").AppendLine(timestamp.ToString());

        if (offset is not null)
        {
            description.AppendLine(string.Format(
                Messages.TimestampOffset, Markdown.InlineCode(offset.ToString() ?? string.Empty))).AppendLine();
        }

        foreach (var markdownTimestamp in AllStyles.Select(style => Markdown.Timestamp(timestamp, style)))
        {
            description.AppendBulletPoint(Markdown.InlineCode(markdownTimestamp))
                .Append(" → ").AppendLine(markdownTimestamp);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.TimestampTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Blue)
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
