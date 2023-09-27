using System.ComponentModel;
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

[UsedImplicitly]
public class ToolsCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestGuildAPI _guildApi;
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

    [Command("showinfo")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows info about user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteShowInfoAsync(
        [Description("Specific user or ID to show info about")]
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

        if (target is not null)
        {
            return await ShowUserInfoAsync(target, currentUser, data, guildId, CancellationToken);
        }

        return await ShowUserInfoAsync(user, currentUser, data, guildId, CancellationToken);
    }

    private async Task<Result> ShowUserInfoAsync(
        IUser user, IUser currentUser, GuildData data, Snowflake guildId, CancellationToken ct = default)
    {
        var embedColor = ColorsList.Cyan;

        var memberData = data.GetOrCreateMemberData(user.ID);

        var guildMemberResult = await _guildApi.GetGuildMemberAsync(guildId, user.ID, ct);
        guildMemberResult.IsDefined(out var guildMember);

        var isMuted = (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil) ||
                      (guildMember is not null && guildMember.CommunicationDisabledUntil.IsDefined());

        var existingBanResult = await _guildApi.GetGuildBanAsync(guildId, user.ID, ct);

        var builder = new StringBuilder().AppendLine($"### <@{user.ID}>");

        if (user.GlobalName is not null)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoDisplayName)
                .Append(" - ").AppendLine(Markdown.Sanitize(user.GlobalName));
        }

        builder.Append("- ").AppendLine(Messages.ShowInfoDiscordUserSince)
            .Append(" - ").AppendLine(Markdown.Timestamp(user.ID.Timestamp));

        if (isMuted || existingBanResult.IsDefined())
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.ShowInfoPunishments));
        }

        if (isMuted)
        {
            ShowInfoMutedUntilAsync(memberData, guildMember, builder);

            embedColor = ColorsList.Red;
        }

        if (existingBanResult.IsDefined())
        {
            ShowInfoBannedUntilAsync(memberData, builder);

            embedColor = ColorsList.Black;
        }

        if (!guildMemberResult.IsSuccess && !existingBanResult.IsDefined())
        {
            builder.Append("### ")
                .AppendLine(Markdown.Bold(Messages.ShowInfoNotOnServer));

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

    private static void ShowInfoBannedUntilAsync(MemberData memberData, StringBuilder builder)
    {
        if (memberData.BannedUntil < DateTimeOffset.MaxValue)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoBanned)
                .Append(" - ").Append(Messages.ShowInfoUntil).Append(' ')
                .AppendLine(Markdown.Timestamp(memberData.BannedUntil.Value));
        }

        if (memberData.BannedUntil >= DateTimeOffset.MaxValue)
        {
            builder.Append("- ").AppendLine(Messages.ShowInfoBannedPermanently);
        }
    }

    private static void ShowInfoMutedUntilAsync(
        MemberData memberData, IGuildMember? guildMember, StringBuilder builder)
    {
        builder.Append("- ").AppendLine(Messages.ShowInfoMuted);
        if (memberData.MutedUntil is not null && DateTimeOffset.UtcNow <= memberData.MutedUntil)
        {
            builder.Append(" - ").AppendLine(Messages.ShowInfoMutedWithMuteRole)
                .Append(" - ").Append(Messages.ShowInfoUntil).Append(' ')
                .AppendLine(Markdown.Timestamp(memberData.MutedUntil.Value));
        }

        if (guildMember is not null && guildMember.CommunicationDisabledUntil.IsDefined())
        {
            builder.Append(" - ").AppendLine(Messages.ShowInfoMutedWithTimeout)
                .Append(" - ").Append(Messages.ShowInfoUntil).Append(' ')
                .AppendLine(Markdown.Timestamp(guildMember.CommunicationDisabledUntil.Value.Value));
        }
    }
}
