using System.Text.Json.Nodes;
using Remora.Rest.Core;

namespace Octobot.Data;

/// <summary>
///     Stores information about a guild. This information is not accessible via the Discord API.
/// </summary>
/// <remarks>This information is stored on disk as a JSON file.</remarks>
public sealed class GuildData
{
    public readonly Dictionary<ulong, MemberData> MemberData;
    public readonly string MemberDataPath;

    public readonly Dictionary<ulong, ScheduledEventData> ScheduledEvents;
    public readonly string ScheduledEventsPath;
    public readonly JsonNode Settings;
    public readonly string SettingsPath;

    public GuildData(
        JsonNode settings, string settingsPath,
        Dictionary<ulong, ScheduledEventData> scheduledEvents, string scheduledEventsPath,
        Dictionary<ulong, MemberData> memberData, string memberDataPath)
    {
        Settings = settings;
        SettingsPath = settingsPath;
        ScheduledEvents = scheduledEvents;
        ScheduledEventsPath = scheduledEventsPath;
        MemberData = memberData;
        MemberDataPath = memberDataPath;
    }

    public MemberData GetOrCreateMemberData(Snowflake userId)
    {
        if (MemberData.TryGetValue(userId.Value, out var existing))
        {
            return existing;
        }

        var newData = new MemberData(userId.Value);
        MemberData.Add(userId.Value, newData);
        return newData;
    }
}
