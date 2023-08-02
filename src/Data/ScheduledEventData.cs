using Remora.Discord.API.Abstractions.Objects;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about scheduled events. This information is not provided by the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public sealed class ScheduledEventData
{
    public ScheduledEventData(GuildScheduledEventStatus status)
    {
        Status = status;
    }

    public bool EarlyNotificationSent { get; set; }
    public DateTimeOffset? ActualStartTime { get; set; }
    public GuildScheduledEventStatus Status { get; set; }
}