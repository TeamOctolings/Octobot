using Boyfriend.Data;

namespace Boyfriend.Commands;

public sealed class RemindCommand : ICommand {
    public string[] Aliases { get; } = { "remind", "reminder", "remindme", "напомни", "напомнить", "напоминание" };

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        // TODO: actually make this good
        var remindIn = CommandProcessor.GetTimeSpan(args, 0);
        if (remindIn.TotalSeconds < 1) {
            cmd.Reply(Messages.InvalidRemindIn, ReplyEmojis.InvalidArgument);
            return Task.CompletedTask;
        }

        var reminderText = cmd.GetRemaining(cleanArgs, 1, "ReminderText");
        if (reminderText is not null) {
            var reminderOffset = DateTimeOffset.UtcNow.Add(remindIn);
            GuildData.Get(cmd.Context.Guild).MemberData[cmd.Context.User.Id].Reminders.Add(
                new Reminder {
                    RemindAt = reminderOffset,
                    ReminderText = reminderText,
                    ReminderChannel = cmd.Context.Channel.Id
                });

            cmd.ConfigWriteScheduled = true;

            var feedback = string.Format(Messages.FeedbackReminderAdded, reminderOffset.ToUnixTimeSeconds().ToString());
            cmd.Reply(feedback, ReplyEmojis.Reminder);
        }

        return Task.CompletedTask;
    }
}
