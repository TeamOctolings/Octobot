using System.Diagnostics;

namespace Octobot.Services.Profiler;

public struct ProfilerEvent
{
    public string Id { get; init; }
    public Stopwatch Stopwatch { get; init; }
}
