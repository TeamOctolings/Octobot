using Boyfriend.Data;

namespace Boyfriend.Commands;

public sealed class RemindCommand : ICommand {
    public string[] Aliases { get; } = { "remind", "reminder", "remindme", "напомни", "напомнить", "напоминание" };

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var remindIn = CommandProcessor.GetTimeSpan(args, 0);
        var reminderText = cmd.GetRemaining(args, 1, "ReminderText");
        if (reminderText is not null)
            GuildData.FromSocketGuild(cmd.Context.Guild).MemberData[cmd.Context.User.Id].Reminders.Add(new Reminder {
                RemindAt = DateTimeOffset.Now.Add(remindIn),
                ReminderText = reminderText
            });

        return Task.CompletedTask;
    }
}
