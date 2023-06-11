using Remora.Discord.API.Abstractions.Objects;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about scheduled events. This information is not provided by the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public class ScheduledEventData {
    public DateTimeOffset?           ActualStartTime;
    public GuildScheduledEventStatus Status;

    public ScheduledEventData(GuildScheduledEventStatus status) {
        Status = status;
    }
}
