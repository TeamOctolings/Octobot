using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Discord.WebSocket;

namespace Boyfriend.Data;

public record GuildData {
    public static readonly Dictionary<string, string> DefaultConfiguration = new() {
        { "Prefix", "!" },
        { "Lang", "en" },
        { "ReceiveStartupMessages", "false" },
        { "WelcomeMessage", "default" },
        { "SendWelcomeMessages", "true" },
        { "PublicFeedbackChannel", "0" },
        { "PrivateFeedbackChannel", "0" },
        { "StarterRole", "0" },
        { "MuteRole", "0" },
        { "RemoveRolesOnMute", "false" },
        { "ReturnRolesOnRejoin", "false" },
        { "EventStartedReceivers", "interested,role" },
        { "EventNotificationRole", "0" },
        { "EventNotificationChannel", "0" },
        { "EventEarlyNotificationOffset", "0" }
    };

    private static readonly Dictionary<ulong, GuildData> GuildDataDictionary = new();

    public readonly Dictionary<ulong, MemberData> MemberData;

    public readonly Dictionary<string, string> Preferences;

    private SocketRole? _cachedMuteRole;

    private ulong _id;

    [SuppressMessage("Performance", "CA1853:Unnecessary call to \'Dictionary.ContainsKey(key)\'")]
    // https://github.com/dotnet/roslyn-analyzers/issues/6377
    private GuildData(SocketGuild guild) {
        var id = guild.Id;
        if (!Directory.Exists($"{id}")) Directory.CreateDirectory($"{id}");
        if (!Directory.Exists($"{id}/MemberData")) Directory.CreateDirectory($"{id}/MemberData");
        if (!File.Exists($"{id}/Configuration.json")) File.Create($"{id}/Configuration.json").Dispose();
        Preferences
            = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText($"{id}/Configuration.json")) ??
              new Dictionary<string, string>();

        // ReSharper disable twice ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        if (Preferences.Keys.Count < DefaultConfiguration.Keys.Count)
            foreach (var key in DefaultConfiguration.Keys)
                if (!Preferences.ContainsKey(key))
                    Preferences.Add(key, DefaultConfiguration[key]);
        if (Preferences.Keys.Count > DefaultConfiguration.Keys.Count)
            foreach (var key in Preferences.Keys)
                if (!DefaultConfiguration.ContainsKey(key))
                    Preferences.Remove(key);
        Preferences.TrimExcess();

        MemberData = new Dictionary<ulong, MemberData>();
        foreach (var data in Directory.GetFiles($"{id}/MemberData")) {
            var deserialised = JsonSerializer.Deserialize<MemberData>(File.ReadAllText($"{id}/MemberData/{data}.json"));
            MemberData.Add(deserialised!.Id, deserialised);
        }

        if (guild.MemberCount > MemberData.Count)
            foreach (var member in guild.Users) {
                if (MemberData.TryGetValue(member.Id, out var memberData)) {
                    if (!memberData.IsInGuild &&
                        DateTimeOffset.Now.ToUnixTimeSeconds() -
                        Math.Max(memberData.LeftAt.Last(), memberData.BannedUntil) >
                        60 * 60 * 24 * 30) {
                        File.Delete($"{id}/MemberData/{memberData.Id}.json");
                        MemberData.Remove(memberData.Id);
                    }

                    continue;
                }

                var data = new MemberData(member);
                MemberData.Add(member.Id, data);
                File.WriteAllText($"{id}/MemberData/{data.Id}.json",
                    JsonSerializer.Serialize(data));
            }

        GuildDataDictionary.Add(id, this);
    }

    public SocketRole? MuteRole {
        get => _cachedMuteRole ??= Boyfriend.Client.GetGuild(_id).Roles
            .Single(x => x.Id == ulong.Parse(Preferences["MuteRole"]));
        set => _cachedMuteRole = value;
    }

    public static GuildData FromSocketGuild(SocketGuild guild) {
        if (GuildDataDictionary.TryGetValue(guild.Id, out var stored)) return stored;
        var newData = new GuildData(guild) {
            _id = guild.Id
        };
        GuildDataDictionary.Add(guild.Id, newData);
        return newData;
    }
}
