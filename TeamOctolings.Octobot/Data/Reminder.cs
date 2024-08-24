namespace TeamOctolings.Octobot.Data;

public sealed record Reminder
{
    public required DateTimeOffset At { get; init; }
    public required string Text { get; init; }
    public required ulong ChannelId { get; init; }
    public required ulong MessageId { get; init; }
}
