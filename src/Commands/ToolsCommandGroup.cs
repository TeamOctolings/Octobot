using System.ComponentModel;
using System.Drawing;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
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
///     Handles tool commands: /showinfo, /random, /timestamp.
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
        if (!_context.TryGetContextIDs(out var guildId, out _, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await SendRandomNumberAsync(first, second, user, CancellationToken);
    }

    private async Task<Result> SendRandomNumberAsync(long first, long? secondNullable,
        IUser user, CancellationToken ct)
    {
        const int secondDefault = 0;
        var second = secondNullable ?? secondDefault;

        var min = Math.Min(first, second);
        var max = Math.Max(first, second);

        var i = Random.Shared.NextInt64(min, max + 1);

        var description = new StringBuilder().Append("# ").Append(i);

        description.AppendLine().Append("- ").Append(string.Format(
            Messages.RandomMin, Markdown.InlineCode(min.ToString())));
        if (secondNullable is null && first >= secondDefault)
        {
            description.Append(' ').Append(Messages.Default);
        }

        description.AppendLine().Append("- ").Append(string.Format(
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
                string.Format(Messages.RandomTitle, user.GetTag()), user)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    private static readonly TimestampStyle[] AllStyles =
    {
        TimestampStyle.ShortDate,
        TimestampStyle.LongDate,
        TimestampStyle.ShortTime,
        TimestampStyle.LongTime,
        TimestampStyle.ShortDateTime,
        TimestampStyle.LongDateTime,
        TimestampStyle.RelativeTime
    };

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
        if (!_context.TryGetContextIDs(out var guildId, out _, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await SendTimestampAsync(offset, user, CancellationToken);
    }

    private async Task<Result> SendTimestampAsync(TimeSpan? offset, IUser user, CancellationToken ct)
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
            description.Append("- ").Append(Markdown.InlineCode(markdownTimestamp))
                .Append(" → ").AppendLine(markdownTimestamp);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.TimestampTitle, user.GetTag()), user)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Blue)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
