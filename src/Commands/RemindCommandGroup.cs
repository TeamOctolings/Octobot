using System.ComponentModel;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
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
    private readonly FeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public RemindCommandGroup(
        ICommandContext context, GuildDataService guildData, FeedbackService feedback,
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

        return await ListRemindersAsync(data.GetOrCreateMemberData(executorId), executor, bot, CancellationToken);
    }

    private async Task<Result> ListRemindersAsync(MemberData data, IUser executor, IUser bot, CancellationToken ct)
    {
        if (data.Reminders.Count == 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.NoRemindersFound, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var builder = new StringBuilder();
        for (var i = data.Reminders.Count - 1; i >= 0; i--)
        {
            var reminder = data.Reminders[i];
            builder.Append("- ").AppendLine(string.Format(Messages.ReminderIndex, Markdown.InlineCode(i.ToString())))
                .Append(" - ").AppendLine(string.Format(Messages.ReminderMessage, Markdown.InlineCode(reminder.Text)))
                .Append(" - ")
                .AppendLine(string.Format(Messages.ReminderWillBeSentOn, Markdown.Timestamp(reminder.At)));
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
    /// <param name="message">The text of the reminder.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("remind")]
    [Description("Create a reminder")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteReminderAsync(
        [Description("After what period of time mention the reminder")]
        TimeSpan @in,
        [Description("Reminder message")] string message)
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

        return await AddReminderAsync(@in, message, data, channelId, executor, CancellationToken);
    }

    private async Task<Result> AddReminderAsync(
        TimeSpan @in, string message, GuildData data,
        Snowflake channelId, IUser executor, CancellationToken ct = default)
    {
        var remindAt = DateTimeOffset.UtcNow.Add(@in);

        data.GetOrCreateMemberData(executor.ID).Reminders.Add(
            new Reminder
            {
                At = remindAt,
                Channel = channelId.Value,
                Text = message
            });

        var builder = new StringBuilder().Append("- ").AppendLine(string.Format(
                Messages.ReminderMessage, Markdown.InlineCode(message)))
            .Append("- ").Append(string.Format(Messages.ReminderWillBeSentOn, Markdown.Timestamp(remindAt)));

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ReminderCreated, executor.GetTag()), executor)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    /// <summary>
    ///     A slash command that deletes a reminder using its index.
    /// </summary>
    /// <param name="index">The index of the reminder to delete.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("delremind")]
    [Description("Delete one of your reminders")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteDeleteReminderAsync(
        [Description("Index of reminder to delete")] [MinValue(0)]
        int index)
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

        return await DeleteReminderAsync(data.GetOrCreateMemberData(executorId), index, bot, CancellationToken);
    }

    private async Task<Result> DeleteReminderAsync(MemberData data, int index, IUser bot,
        CancellationToken ct)
    {
        if (index >= data.Reminders.Count)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.InvalidReminderIndex, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        data.Reminders.RemoveAt(index);

        var embed = new EmbedBuilder().WithSmallTitle(Messages.ReminderDeleted, bot)
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(
            embed, ct);
    }
}
