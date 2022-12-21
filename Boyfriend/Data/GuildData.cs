using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Boyfriend.Data;

public struct GuildData {
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

    public readonly Dictionary<string, string> GuildConfiguration;

    public readonly Dictionary<ulong, MemberData> MemberData;

    /*public static Dictionary<string, string> GetGuildConfig(ulong id) {
        if (GuildConfigDictionary.TryGetValue(id, out var cfg)) return cfg;

        var path = $"config_{id}.json";

        if (!File.Exists(path)) File.Create(path).Dispose();

        var json = File.ReadAllText(path);
        var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();

        if (config.Keys.Count < GuildData.DefaultConfiguration.Keys.Count) {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // Conversion will result in a lot of memory allocations
            foreach (var key in GuildData.DefaultConfiguration.Keys)
                if (!config.ContainsKey(key))
                    config.Add(key, GuildData.DefaultConfiguration[key]);
        } else if (config.Keys.Count > GuildData.DefaultConfiguration.Keys.Count) {
            foreach (var key in config.Keys.Where(key => !GuildData.DefaultConfiguration.ContainsKey(key))) config.Remove(key);
        }

        GuildConfigDictionary.Add(id, config);

        return config;
    }*/

    /*public static async Task WriteGuildConfigAsync(ulong id) {
        await File.WriteAllTextAsync($"config_{id}.json",
            JsonConvert.SerializeObject(GuildConfigDictionary[id], Formatting.Indented));

        if (RemovedRolesDictionary.TryGetValue(id, out var removedRoles))
            await File.WriteAllTextAsync($"removedroles_{id}.json",
                JsonConvert.SerializeObject(removedRoles, Formatting.Indented));
    }*/
    [SuppressMessage("Performance", "CA1853:Unnecessary call to \'Dictionary.ContainsKey(key)\'")]
    // https://github.com/dotnet/roslyn-analyzers/issues/6377
    public GuildData(SocketGuild guild) {
        var id = guild.Id;
        if (GuildDataDictionary.TryGetValue(id, out var stored)) {
            this = stored;
            return;
        }

        if (!Directory.Exists($"{id}")) Directory.CreateDirectory($"{id}");
        if (!Directory.Exists($"{id}/MemberData")) Directory.CreateDirectory($"{id}/MemberData");
        if (!File.Exists($"{id}/Configuration.json")) File.Create($"{id}/Configuration.json").Dispose();
        GuildConfiguration = JsonConvert.DeserializeObject<Dictionary<string, string>>($"{id}/Configuration.json") ??
                             new Dictionary<string, string>();

        // ReSharper disable twice ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        if (GuildConfiguration.Keys.Count < DefaultConfiguration.Keys.Count)
            foreach (var key in DefaultConfiguration.Keys)
                if (!GuildConfiguration.ContainsKey(key))
                    GuildConfiguration.Add(key, DefaultConfiguration[key]);
        if (GuildConfiguration.Keys.Count > DefaultConfiguration.Keys.Count)
            foreach (var key in GuildConfiguration.Keys)
                if (!DefaultConfiguration.ContainsKey(key))
                    GuildConfiguration.Remove(key);
        GuildConfiguration.TrimExcess();

        MemberData = new Dictionary<ulong, MemberData>();
        foreach (var data in Directory.GetFiles($"{id}/MemberData")) {
            var deserialised = JsonConvert.DeserializeObject<MemberData>($"{id}/MemberData/{data}.json") ??
                               throw new UnreachableException();
            MemberData.Add(deserialised.Id, deserialised);
        }

        if (guild.MemberCount > MemberData.Count)
            foreach (var member in guild.Users) {
                if (MemberData.TryGetValue(member.Id, out var memberData)) {
                    if (memberData is { IsInGuild: false } &&
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
                    JsonConvert.SerializeObject(data, Formatting.Indented));
            }

        GuildDataDictionary.Add(id, this);
    }
}
