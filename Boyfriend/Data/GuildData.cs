using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Discord.WebSocket;

namespace Boyfriend.Data;

public record GuildData {
    public static readonly Dictionary<string, string> DefaultPreferences = new() {
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
        // TODO: { "AutoStartEvents", "false" }
    };

    public static readonly Dictionary<ulong, GuildData> GuildDataDictionary = new();

    public readonly Dictionary<ulong, MemberData> MemberData;

    public readonly Dictionary<string, string> Preferences;

    private SocketRole? _cachedMuteRole;

    private ulong _id;

    [SuppressMessage("Performance", "CA1853:Unnecessary call to \'Dictionary.ContainsKey(key)\'")]
    // https://github.com/dotnet/roslyn-analyzers/issues/6377
    private GuildData(SocketGuild guild) {
        if (!Directory.Exists($"{_id}")) Directory.CreateDirectory($"{_id}");
        if (!Directory.Exists($"{_id}/MemberData")) Directory.CreateDirectory($"{_id}/MemberData");
        if (!File.Exists($"{_id}/Configuration.json")) File.Create($"{_id}/Configuration.json").Dispose();
        Preferences
            = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText($"{_id}/Configuration.json")) ??
              new Dictionary<string, string>();

        if (Preferences.Keys.Count < DefaultPreferences.Keys.Count)
            foreach (var key in DefaultPreferences.Keys.Where(key => !Preferences.ContainsKey(key)))
                Preferences.Add(key, DefaultPreferences[key]);
        if (Preferences.Keys.Count > DefaultPreferences.Keys.Count)
            foreach (var key in Preferences.Keys.Where(key => !DefaultPreferences.ContainsKey(key)))
                Preferences.Remove(key);
        Preferences.TrimExcess();

        MemberData = new Dictionary<ulong, MemberData>();
        foreach (var data in Directory.GetFiles($"{_id}/MemberData")) {
            var deserialised
                = JsonSerializer.Deserialize<MemberData>(File.ReadAllText($"{_id}/MemberData/{data}.json"));
            MemberData.Add(deserialised!.Id, deserialised);
        }


        foreach (var member in guild.Users) {
            if (MemberData.TryGetValue(member.Id, out var memberData)) {
                if (!memberData.IsInGuild &&
                    DateTimeOffset.Now.ToUnixTimeSeconds() -
                    Math.Max(memberData.LeftAt.Last().ToUnixTimeSeconds(), memberData.BannedUntil.ToUnixTimeSeconds()) >
                    60 * 60 * 24 * 30) {
                    File.Delete($"{_id}/MemberData/{memberData.Id}.json");
                    MemberData.Remove(memberData.Id);
                }

                continue;
            }

            MemberData.Add(member.Id, new MemberData(member));
        }

        MemberData.TrimExcess();
    }

    public SocketRole? MuteRole {
        get {
            if (Preferences["MuteRole"] is "0") return null;
            return _cachedMuteRole ??= Boyfriend.Client.GetGuild(_id).Roles
                .Single(x => x.Id == ulong.Parse(Preferences["MuteRole"]));
        }
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

    public async Task Save(bool saveMemberData) {
        Preferences.TrimExcess();
        await File.WriteAllTextAsync($"{_id}/Configuration.json",
            JsonSerializer.Serialize(Preferences));
        if (saveMemberData)
            foreach (var data in MemberData.Values)
                await File.WriteAllTextAsync($"{_id}/MemberData/{data.Id}.json",
                    JsonSerializer.Serialize(data));
    }
}
