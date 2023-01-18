namespace Boyfriend.Data;

public struct Reminder {
    public DateTimeOffset RemindAt;
    public string ReminderText;
    public ulong ReminderChannel;
}
