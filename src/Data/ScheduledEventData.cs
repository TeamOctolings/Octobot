using Remora.Discord.API.Abstractions.Objects;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about scheduled events. This information is not provided by the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public sealed class ScheduledEventData
{
    public ScheduledEventData(ulong id, string name, GuildScheduledEventStatus status,
        DateTimeOffset scheduledStartTime)
    {
        Id = id;
        Name = name;
        Status = status;
        ScheduledStartTime = scheduledStartTime;
    }

    public ulong Id { get; }
    public string Name { get; set; }
    public bool EarlyNotificationSent { get; set; }
    public DateTimeOffset ScheduledStartTime { get; set; }
    public DateTimeOffset? ActualStartTime { get; set; }
    public GuildScheduledEventStatus? Status { get; set; }
    public bool ScheduleOnStatusUpdated { get; set; } = true;
}
