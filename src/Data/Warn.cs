using Remora.Rest.Core;

namespace Octobot.Data;

public struct Warn
{
    public ulong WarnedBy { get; init; }
    public DateTimeOffset At { get; init; }
    public string Reason { get; init; }
}
