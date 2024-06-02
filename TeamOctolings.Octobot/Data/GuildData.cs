using System.Text.Json.Nodes;
using Remora.Rest.Core;

namespace TeamOctolings.Octobot.Data;

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

    public readonly bool DataLoadFailed;

    public GuildData(
        JsonNode settings, string settingsPath,
        Dictionary<ulong, ScheduledEventData> scheduledEvents, string scheduledEventsPath,
        Dictionary<ulong, MemberData> memberData, string memberDataPath, bool dataLoadFailed)
    {
        Settings = settings;
        SettingsPath = settingsPath;
        ScheduledEvents = scheduledEvents;
        ScheduledEventsPath = scheduledEventsPath;
        MemberData = memberData;
        MemberDataPath = memberDataPath;
        DataLoadFailed = dataLoadFailed;
    }

    public MemberData GetOrCreateMemberData(Snowflake memberId)
    {
        if (MemberData.TryGetValue(memberId.Value, out var existing))
        {
            return existing;
        }

        var newData = new MemberData(memberId.Value);
        MemberData.Add(memberId.Value, newData);
        return newData;
    }
}
