using System.Globalization;

namespace Boyfriend.Data;

public class GuildData {
    public readonly GuildConfiguration Configuration;
    public readonly string ConfigurationPath;

    public readonly Dictionary<ulong, ScheduledEventData> ScheduledEvents;
    public readonly string ScheduledEventsPath;

    public GuildData(
        GuildConfiguration                    configuration,   string configurationPath,
        Dictionary<ulong, ScheduledEventData> scheduledEvents, string scheduledEventsPath) {
        Configuration = configuration;
        ConfigurationPath = configurationPath;
        ScheduledEvents = scheduledEvents;
        ScheduledEventsPath = scheduledEventsPath;
    }

    public CultureInfo Culture => Configuration.Culture;
}
