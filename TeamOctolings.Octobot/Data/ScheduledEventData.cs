using System.Text.Json.Serialization;
using Remora.Discord.API.Abstractions.Objects;

namespace TeamOctolings.Octobot.Data;

/// <summary>
///     Stores information about scheduled events. This information is not provided by the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public sealed class ScheduledEventData
{
    public ScheduledEventData(ulong id, string name, DateTimeOffset scheduledStartTime,
        GuildScheduledEventStatus status)
    {
        Id = id;
        Name = name;
        ScheduledStartTime = scheduledStartTime;
        Status = status;
    }

    [JsonConstructor]
    public ScheduledEventData(ulong id, string name, bool earlyNotificationSent, DateTimeOffset scheduledStartTime,
        DateTimeOffset? actualStartTime, GuildScheduledEventStatus? status, bool scheduleOnStatusUpdated)
    {
        Id = id;
        Name = name;
        EarlyNotificationSent = earlyNotificationSent;
        ScheduledStartTime = scheduledStartTime;
        ActualStartTime = actualStartTime;
        Status = status;
        ScheduleOnStatusUpdated = scheduleOnStatusUpdated;
    }

    public ulong Id { get; }
    public string Name { get; set; }
    public bool EarlyNotificationSent { get; set; }
    public DateTimeOffset ScheduledStartTime { get; set; }
    public DateTimeOffset? ActualStartTime { get; set; }
    public GuildScheduledEventStatus? Status { get; set; }
    public bool ScheduleOnStatusUpdated { get; set; } = true;
}
