using System.ComponentModel;
using System.Drawing;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Parsers;
using Octobot.Services;
using Octobot.Services.Profiler;
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
    private readonly Profiler _profiler;

    public ToolsCommandGroup(
        ICommandContext context, IFeedbackService feedback,
        GuildDataService guildData, IDiscordRestGuildAPI guildApi,
        IDiscordRestUserAPI userApi, IDiscordRestChannelAPI channelApi, Profiler profiler)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _profiler = profiler;
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
        _profiler.Push("userinfo_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Push("current_user_get");
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        _profiler.Pop();
        _profiler.Push("guild_settings_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await ShowUserInfoAsync(target ?? executor, bot, data, guildId, CancellationToken));
    }

    private async Task<Result> ShowUserInfoAsync(
        IUser target, IUser bot, GuildData data, Snowflake guildId, CancellationToken ct = default)
    {
        _profiler.Push("main");
        _profiler.Push("builder_construction");
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
            _profiler.Push("append_guild");
            communicationDisabledUntil = guildMember.CommunicationDisabledUntil.OrDefault(null);

            embedColor = AppendGuildInformation(embedColor, guildMember, builder);
            _profiler.Pop();
        }

        var wasMuted = (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil) ||
                       communicationDisabledUntil is not null;
        var wasBanned = memberData.BannedUntil is not null;
        var wasKicked = memberData.Kicked;

        if (wasMuted || wasBanned || wasKicked)
        {
            _profiler.Push("append_punishments");
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.UserInfoPunishments));

            embedColor = AppendPunishmentsInformation(wasMuted, wasKicked, wasBanned, memberData,
                builder, embedColor, communicationDisabledUntil);
            _profiler.Pop();
        }

        if (!guildMemberResult.IsSuccess && !wasBanned)
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.UserInfoNotOnGuild));

            embedColor = ColorsList.Default;
        }

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.InformationAbout, target.GetTag()), bot)
            .WithDescription(builder.ToString())
            .WithColour(embedColor)
            .WithLargeUserAvatar(target)
            .WithFooter($"ID: {target.ID.ToString()}")
            .Build();

        _profiler.Pop();
        return await _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
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
    [Description("Shows info current guild")]
    [UsedImplicitly]
    public async Task<Result> ExecuteGuildInfoAsync()
    {
        _profiler.Push("guildinfo_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Push("current_user_get");
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        _profiler.Pop();
        _profiler.Push("guild_get");
        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result.FromError(guildResult);
        }

        _profiler.Pop();
        _profiler.Push("guild_settings_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await ShowGuildInfoAsync(bot, guild, CancellationToken));
    }

    private Task<Result> ShowGuildInfoAsync(IUser bot, IGuild guild, CancellationToken ct)
    {
        _profiler.Push("main");
        _profiler.Push("builder_construction");
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

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.InformationAbout, guild.Name), bot)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .WithLargeGuildIcon(guild)
            .WithGuildBanner(guild)
            .WithFooter($"ID: {guild.ID.ToString()}")
            .Build();

        _profiler.Pop();
        return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
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
        _profiler.Push("userinfo_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        _profiler.Pop();
        _profiler.Push("guild_settings_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await SendRandomNumberAsync(first, second, executor, CancellationToken));
    }

    private Task<Result> SendRandomNumberAsync(long first, long? secondNullable,
        IUser executor, CancellationToken ct)
    {
        _profiler.Push("main");
        _profiler.Push("random_number_get");
        const long secondDefault = 0;
        var second = secondNullable ?? secondDefault;

        var min = Math.Min(first, second);
        var max = Math.Max(first, second);

        var i = Random.Shared.NextInt64(min, max + 1);

        _profiler.Pop();
        _profiler.Push("builder_construction");
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

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.RandomTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .Build();

        _profiler.Pop();
        return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
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
    /// <param name="stringOffset">The offset for the current timestamp.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("timestamp")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows a timestamp in all styles")]
    [UsedImplicitly]
    public async Task<Result> ExecuteTimestampAsync(
        [Description("Offset from current time")] [Option("offset")]
        string? stringOffset = null)
    {
        _profiler.Push("timestamp_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Push("current_user_get");
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        _profiler.Pop();
        _profiler.Push("guild_settings_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        _profiler.Pop();
        if (stringOffset is null)
        {
            _profiler.Pop();
            return _profiler.ReportWithResult(await SendTimestampAsync(null, executor, CancellationToken));
        }

        _profiler.Push("parse_input");
        var parseResult = TimeSpanParser.TryParse(stringOffset);
        if (!parseResult.IsDefined(out var offset))
        {
            var failedEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.InvalidTimeSpan, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: CancellationToken);
        }

        _profiler.Pop();
        _profiler.Pop();
        return _profiler.ReportWithResult(await SendTimestampAsync(offset, executor, CancellationToken));
    }

    private Task<Result> SendTimestampAsync(TimeSpan? offset, IUser executor, CancellationToken ct)
    {
        _profiler.Push("main");
        _profiler.Push("builder_construction");
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

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.TimestampTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Blue)
            .Build();

        _profiler.Pop();
        return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }
}
