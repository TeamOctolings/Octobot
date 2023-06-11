using System.Globalization;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about a guild. This information is not accessible via the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public class GuildData {
    public readonly GuildConfiguration Configuration;
    public readonly string             ConfigurationPath;

    public readonly Dictionary<ulong, ScheduledEventData> ScheduledEvents;
    public readonly string                                ScheduledEventsPath;

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
