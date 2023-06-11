using System.Globalization;
using Remora.Rest.Core;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about a guild. This information is not accessible via the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public class GuildData {
    public readonly GuildConfiguration Configuration;
    public readonly string             ConfigurationPath;

    public readonly Dictionary<ulong, MemberData> MemberData;
    public readonly string                        MemberDataPath;

    public readonly Dictionary<ulong, ScheduledEventData> ScheduledEvents;
    public readonly string                                ScheduledEventsPath;

    public GuildData(
        GuildConfiguration                    configuration,   string configurationPath,
        Dictionary<ulong, ScheduledEventData> scheduledEvents, string scheduledEventsPath,
        Dictionary<ulong, MemberData>         memberData,      string memberDataPath) {
        Configuration = configuration;
        ConfigurationPath = configurationPath;
        ScheduledEvents = scheduledEvents;
        ScheduledEventsPath = scheduledEventsPath;
        MemberData = memberData;
        MemberDataPath = memberDataPath;
    }

    public CultureInfo Culture => Configuration.GetCulture();

    public MemberData GetMemberData(Snowflake userId) {
        if (MemberData.TryGetValue(userId.Value, out var existing)) return existing;

        var newData = new MemberData(userId.Value, null);
        MemberData.Add(userId.Value, newData);
        return newData;
    }
}
