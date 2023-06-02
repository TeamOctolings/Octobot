using Remora.Discord.API.Abstractions.Objects;

namespace Boyfriend.Data;

public class ScheduledEventData {
    public DateTimeOffset? ActualStartTime;
    public GuildScheduledEventStatus Status;

    public ScheduledEventData(GuildScheduledEventStatus status) {
        Status = status;
    }
}
