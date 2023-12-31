using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Octobot.Services.Profiler;
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
using Octobot.Parsers;

namespace Octobot.Commands;

/// <summary>
///     Handles commands to manage reminders: /remind, /listremind, /delremind
/// </summary>
[UsedImplicitly]
public class RemindCommandGroup : CommandGroup
{
    private readonly IInteractionCommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestInteractionAPI _interactionApi;
    private readonly Profiler _profiler;
    private readonly IDiscordRestUserAPI _userApi;

    public RemindCommandGroup(
        IInteractionCommandContext context, GuildDataService guildData, IFeedbackService feedback,
        IDiscordRestUserAPI userApi, IDiscordRestInteractionAPI interactionApi, Profiler profiler)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
        _interactionApi = interactionApi;
        _profiler = profiler;
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
    public async Task<Result> ExecuteListRemindersAsync()
    {
        _profiler.Push("list_reminders_command");
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
            return _profiler.ReportWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return _profiler.ReportWithResult(Result.FromError(executorResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await ListRemindersAsync(data.GetOrCreateMemberData(executorId), guildId,
            executor, bot,
            CancellationToken));
    }

    private Task<Result> ListRemindersAsync(MemberData data, Snowflake guildId, IUser executor, IUser bot,
        CancellationToken ct)
    {
        _profiler.Push("main");
        if (data.Reminders.Count == 0)
        {
            _profiler.Push("no_reminders_send");
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.NoRemindersFound, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct));
        }

        _profiler.Push("builder_construction");
        var builder = new StringBuilder();
        for (var i = 0; i < data.Reminders.Count; i++)
        {
            var reminder = data.Reminders[i];
            builder.AppendBulletPointLine(string.Format(Messages.ReminderPosition,
                    Markdown.InlineCode((i + 1).ToString())))
                .AppendSubBulletPointLine(string.Format(Messages.ReminderText, Markdown.InlineCode(reminder.Text)))
                .AppendSubBulletPointLine(string.Format(Messages.ReminderTime, Markdown.Timestamp(reminder.At)))
                .AppendSubBulletPointLine(string.Format(Messages.DescriptionActionJumpToMessage,
                    $"https://discord.com/channels/{guildId.Value}/{reminder.ChannelId}/{reminder.MessageId}"));
        }

        _profiler.Pop();
        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ReminderList, executor.GetTag()), executor)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Cyan)
            .Build();

        return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }

    /// <summary>
    ///     A slash command that schedules a reminder with the specified text.
    /// </summary>
    /// <param name="timeSpanString">The period of time which must pass before the reminder will be sent.</param>
    /// <param name="text">The text of the reminder.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("remind")]
    [Description("Create a reminder")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecuteReminderAsync(
        [Description("After what period of time mention the reminder")]
        [Option("in")]
        string timeSpanString,
        [Description("Reminder text")] [MaxLength(512)]
        string text)
    {
        _profiler.Push("reminder_command");
        _profiler.Push("preparation");
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        _profiler.Push("executor_get");
        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return _profiler.ReportWithResult(Result.FromError(executorResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        var parseResult = TimeSpanParser.TryParse(timeSpanString);
        if (!parseResult.IsDefined(out var timeSpan))
        {
            var failedEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.InvalidTimeSpan, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: CancellationToken);
        }
        _profiler.Pop();
        return _profiler.ReportWithResult(await AddReminderAsync(@in, text, data, channelId, executor,
            CancellationToken));
    }

        return await AddReminderAsync(timeSpan, text, data, channelId, executor, CancellationToken);
    }

    private async Task<Result> AddReminderAsync(TimeSpan timeSpan, string text, GuildData data,
        Snowflake channelId, IUser executor, CancellationToken ct = default)
    {
        _profiler.Push("main");
        var memberData = data.GetOrCreateMemberData(executor.ID);
        var remindAt = DateTimeOffset.UtcNow.Add(@in);

        _profiler.Push("original_response_get");
        var responseResult =
            await _interactionApi.GetOriginalInteractionResponseAsync(_context.Interaction.ApplicationID,
                _context.Interaction.Token, ct);
        if (!responseResult.IsDefined(out var response))
        {
            return _profiler.PopWithResult(Result.FromError(responseResult));
        }

        _profiler.Pop();

        memberData.Reminders.Add(
            new Reminder
            {
                At = remindAt,
                ChannelId = channelId.Value,
                Text = text,
                MessageId = response.ID.Value
            });

        _profiler.Push("embed_send");
        var builder = new StringBuilder()
            .AppendBulletPointLine(string.Format(Messages.ReminderText, Markdown.InlineCode(text)))
            .AppendBulletPoint(string.Format(Messages.ReminderTime, Markdown.Timestamp(remindAt)));
        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.ReminderCreated, executor.GetTag()), executor)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .WithFooter(string.Format(Messages.ReminderPosition, memberData.Reminders.Count))
            .Build();

        return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(embed, ct: ct));
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
        _profiler.Push("delete_reminder_command");
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
            return _profiler.ReportWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_data_get");
        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await DeleteReminderAsync(data.GetOrCreateMemberData(executorId),
            position - 1, bot, CancellationToken));
    }

    private Task<Result> DeleteReminderAsync(MemberData data, int index, IUser bot,
        CancellationToken ct)
    {
        _profiler.Push("main");
        if (index >= data.Reminders.Count)
        {
            _profiler.Push("invalid_position_send");
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.InvalidReminderPosition, bot)
                .WithColour(ColorsList.Red)
                .Build();

            return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct));
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

        return _profiler.PopWithResult(_feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }
}
