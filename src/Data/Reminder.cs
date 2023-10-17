namespace Octobot.Data;

public struct Reminder
{
    public DateTimeOffset At { get; init; }
    public string Text { get; init; }
    public ulong Channel { get; init; }
}
