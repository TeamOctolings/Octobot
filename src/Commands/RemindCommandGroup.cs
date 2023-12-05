using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Commands;

/// <summary>
///     Handles commands to manage reminders: /remind, /listremind, /delremind
/// </summary>
[UsedImplicitly]
public class RemindCommandGroup : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public RemindCommandGroup(
        ICommandContext context, GuildDataService guildData, IFeedbackService feedback,
        IDiscordRestUserAPI userApi)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that lists reminders of the user that called it.
    /// </summary>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("listremind")]
    [Description("List your reminders")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteListReminderAsync()
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

        return await ListRemindersAsync(data.GetOrCreateMemberData(executorId), guildId, executor, bot, CancellationToken);
    }

    private async Task<Result> ListRemindersAsync(MemberData data, Snowflake guildId, IUser executor, IUser bot, CancellationToken ct)
    {
        if (data.Reminders.Count == 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.NoRemindersFound, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < data.Reminders.Count; i++)
        {
            var reminder = data.Reminders[i];
            builder.AppendBulletPointLine(string.Format(Messages.ReminderPosition, Markdown.InlineCode((i + 1).ToString())))
                .AppendSubBulletPointLine(string.Format(Messages.ReminderText, Markdown.InlineCode(reminder.Text)))
                .AppendSubBulletPointLine(string.Format(Messages.ReminderTime, Markdown.Timestamp(reminder.At)))
                .AppendSubBulletPointLine(string.Format(Messages.DescriptionActionJumpToMessage, $"https://discord.com/channels/{guildId.Value}/{reminder.ChannelId}/{reminder.MessageId}"));
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ReminderList, executor.GetTag()), executor)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Cyan)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(
            embed, ct);
    }

    /// <summary>
    ///     A slash command that schedules a reminder with the specified text.
    /// </summary>
    /// <param name="in">The period of time which must pass before the reminder will be sent.</param>
    /// <param name="text">The text of the reminder.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("remind")]
    [Description("Create a reminder")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteReminderAsync(
        [Description("After what period of time mention the reminder")]
        TimeSpan @in,
        [Description("Reminder text")] [MaxLength(512)]
        string text)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
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

        return await AddReminderAsync(@in, text, data, channelId, executor, CancellationToken);
    }

    private async Task<Result> AddReminderAsync(
        TimeSpan @in, string text, GuildData data,
        Snowflake channelId, IUser executor, CancellationToken ct = default)
    {
        var remindAt = DateTimeOffset.UtcNow.Add(@in);
        var memberData = data.GetOrCreateMemberData(executor.ID);

        var builder = new StringBuilder().AppendBulletPointLine(string.Format(
                Messages.ReminderText, Markdown.InlineCode(text)))
            .AppendBulletPoint(string.Format(Messages.ReminderTime, Markdown.Timestamp(remindAt)));

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ReminderCreated, executor.GetTag()), executor)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .WithFooter(string.Format(Messages.ReminderPosition, memberData.Reminders.Count + 1))
            .Build();
        var messageResult = await _feedback.SendContextualEmbedAsync(embed.Entity, ct: ct);
        if (!messageResult.IsDefined(out var message))
        {
            return (Result)messageResult;
        }

        var messageId = message.ID.Value;
        memberData.Reminders.Add(
            new Reminder
            {
                At = remindAt,
                ChannelId = channelId.Value,
                Text = text,
                MessageId = messageId
            });
        return (Result)messageResult;
    }

    /// <summary>
    ///     A slash command that deletes a reminder using its list position.
    /// </summary>
    /// <param name="position">The list position of the reminder to delete.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("delremind")]
    [Description("Delete one of your reminders")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteDeleteReminderAsync(
        [Description("Position in list")] [MinValue(1)]
        int position)
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

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await DeleteReminderAsync(data.GetOrCreateMemberData(executorId), position - 1, bot, CancellationToken);
    }

    private async Task<Result> DeleteReminderAsync(MemberData data, int index, IUser bot,
        CancellationToken ct)
    {
        if (index >= data.Reminders.Count)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.InvalidReminderPosition, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var reminder = data.Reminders[index];

        var description = new StringBuilder()
            .AppendBulletPointLine(string.Format(Messages.ReminderText, Markdown.InlineCode(reminder.Text)))
            .AppendBulletPointLine(string.Format(Messages.ReminderTime, Markdown.Timestamp(reminder.At)));

        data.Reminders.RemoveAt(index);

        var embed = new EmbedBuilder().WithSmallTitle(Messages.ReminderDeleted, bot)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(
            embed, ct);
    }
}
