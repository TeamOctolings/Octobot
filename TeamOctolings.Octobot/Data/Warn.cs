namespace TeamOctolings.Octobot.Data;

public struct Warn
{
    public ulong WarnedBy { get; init; }
    public DateTimeOffset At { get; init; }
    public string Reason { get; init; }
}
