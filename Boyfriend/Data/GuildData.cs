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
        { "EventEarlyNotificationOffset", "0" },
        { "AutoStartEvents", "false" }
    };

    public static readonly Dictionary<ulong, GuildData> GuildDataDictionary = new();

    private readonly string _configurationFile;

    public readonly List<ulong> EarlyNotifications = new();

    public readonly Dictionary<ulong, MemberData> MemberData;

    public readonly Dictionary<string, string> Preferences;

    private SocketRole? _cachedMuteRole;
    private SocketTextChannel? _cachedPrivateFeedbackChannel;
    private SocketTextChannel? _cachedPublicFeedbackChannel;

    private ulong _id;

    [SuppressMessage("Performance", "CA1853:Unnecessary call to \'Dictionary.ContainsKey(key)\'")]
    // https://github.com/dotnet/roslyn-analyzers/issues/6377
    private GuildData(SocketGuild guild) {
        var idString = $"{_id}";
        var memberDataDir = $"{_id}/MemberData";
        _configurationFile = $"{_id}/Configuration.json";
        if (!Directory.Exists(idString)) Directory.CreateDirectory(idString);
        if (!Directory.Exists(memberDataDir)) Directory.CreateDirectory(memberDataDir);
        if (!File.Exists(_configurationFile)) File.Create(_configurationFile).Dispose();
        Preferences
            = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_configurationFile)) ??
              new Dictionary<string, string>();

        if (Preferences.Keys.Count < DefaultPreferences.Keys.Count)
            foreach (var key in DefaultPreferences.Keys.Where(key => !Preferences.ContainsKey(key)))
                Preferences.Add(key, DefaultPreferences[key]);
        if (Preferences.Keys.Count > DefaultPreferences.Keys.Count)
            foreach (var key in Preferences.Keys.Where(key => !DefaultPreferences.ContainsKey(key)))
                Preferences.Remove(key);
        Preferences.TrimExcess();

        MemberData = new Dictionary<ulong, MemberData>();
        foreach (var data in Directory.GetFiles(memberDataDir)) {
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

    public SocketTextChannel? PublicFeedbackChannel {
        get {
            if (Preferences["PublicFeedbackChannel"] is "0") return null;
            return _cachedPublicFeedbackChannel ??= Boyfriend.Client.GetGuild(_id).TextChannels
                .Single(x => x.Id == ulong.Parse(Preferences["PublicFeedbackChannel"]));
        }
        set => _cachedPublicFeedbackChannel = value;
    }

    public SocketTextChannel? PrivateFeedbackChannel {
        get {
            if (Preferences["PublicFeedbackChannel"] is "0") return null;
            return _cachedPrivateFeedbackChannel ??= Boyfriend.Client.GetGuild(_id).TextChannels
                .Single(x => x.Id == ulong.Parse(Preferences["PrivateFeedbackChannel"]));
        }
        set => _cachedPrivateFeedbackChannel = value;
    }

    public static GuildData Get(SocketGuild guild) {
        if (GuildDataDictionary.TryGetValue(guild.Id, out var stored)) return stored;
        var newData = new GuildData(guild) {
            _id = guild.Id
        };
        GuildDataDictionary.Add(guild.Id, newData);
        return newData;
    }

    public async Task Save(bool saveMemberData) {
        Preferences.TrimExcess();
        await File.WriteAllTextAsync(_configurationFile,
            JsonSerializer.Serialize(Preferences));
        if (saveMemberData)
            foreach (var data in MemberData.Values)
                await File.WriteAllTextAsync($"{_id}/MemberData/{data.Id}.json",
                    JsonSerializer.Serialize(data));
    }
}
