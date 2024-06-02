namespace TeamOctolings.Octobot.Data;

public struct Reminder
{
    public DateTimeOffset At { get; init; }
    public string Text { get; init; }
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
}
