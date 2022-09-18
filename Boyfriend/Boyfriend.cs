using System.Collections.ObjectModel;
using System.Text;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Boyfriend;

public static class Boyfriend {
    public static readonly StringBuilder StringBuilder = new();
    private static readonly Dictionary<ulong, SocketGuild> GuildCache = new();

    private static readonly DiscordSocketConfig Config = new() {
        MessageCacheSize = 250,
        GatewayIntents = GatewayIntents.All,
        AlwaysDownloadUsers = true,
        AlwaysResolveStickers = false,
        AlwaysDownloadDefaultStickers = false,
        LargeThreshold = 500
    };

    public static readonly DiscordSocketClient Client = new(Config);
    private static readonly Game Activity = new("UNDEAD CORPORATION - Everything will freeze", ActivityType.Listening);

    private static readonly Dictionary<ulong, Dictionary<string, string>> GuildConfigDictionary = new();

    private static readonly Dictionary<ulong, Dictionary<ulong, ReadOnlyCollection<ulong>>> RemovedRolesDictionary =
        new();

    public static readonly Dictionary<string, string> DefaultConfig = new() {
        { "Lang", "en" },
        { "Prefix", "!" },
        { "RemoveRolesOnMute", "false" },
        { "SendWelcomeMessages", "true" },
        { "ReceiveStartupMessages", "false" },
        { "FrowningFace", "true" },
        { "WelcomeMessage", Messages.DefaultWelcomeMessage },
        { "EventStartedReceivers", "interested,role" },
        { "StarterRole", "0" },
        { "MuteRole", "0" },
        { "EventNotifyReceiverRole", "0" },
        { "AdminLogChannel", "0" },
        { "BotLogChannel", "0" },
        { "EventCreatedChannel", "0" },
        { "EventStartedChannel", "0" },
        { "EventCancelledChannel", "0" },
        { "EventCompletedChannel", "0" }
    };

    public static void Main() {
        Init().GetAwaiter().GetResult();
    }

    private static async Task Init() {
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();
        await Client.SetActivityAsync(Activity);

        new EventHandler().InitEvents();

        await Task.Delay(-1);
    }

    private static Task Log(LogMessage msg) {
        Console.WriteLine(msg.ToString());

        return Task.CompletedTask;
    }

    public static async Task WriteGuildConfigAsync(ulong id) {
        var json = JsonConvert.SerializeObject(GuildConfigDictionary[id], Formatting.Indented);
        var removedRoles = JsonConvert.SerializeObject(RemovedRolesDictionary[id], Formatting.Indented);

        await File.WriteAllTextAsync($"config_{id}.json", json);
        await File.WriteAllTextAsync($"removedroles_{id}.json", removedRoles);
    }

    public static Dictionary<string, string> GetGuildConfig(ulong id) {
        if (!RemovedRolesDictionary.ContainsKey(id))
            RemovedRolesDictionary.Add(id, new Dictionary<ulong, ReadOnlyCollection<ulong>>());

        if (GuildConfigDictionary.ContainsKey(id)) return GuildConfigDictionary[id];

        var path = $"config_{id}.json";

        if (!File.Exists(path)) File.Create(path).Dispose();

        var json = File.ReadAllText(path);
        var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();

        if (config.Keys.Count < DefaultConfig.Keys.Count) {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // Avoids a closure allocation with the config variable
            foreach (var key in DefaultConfig.Keys)
                if (!config.ContainsKey(key))
                    config.Add(key, DefaultConfig[key]);
        } else if (config.Keys.Count > DefaultConfig.Keys.Count) {
            foreach (var key in config.Keys.Where(key => !DefaultConfig.ContainsKey(key)))
                config.Remove(key);
        }

        GuildConfigDictionary.Add(id, config);

        return config;
    }

    public static Dictionary<ulong, ReadOnlyCollection<ulong>> GetRemovedRoles(ulong id) {
        if (RemovedRolesDictionary.ContainsKey(id)) return RemovedRolesDictionary[id];

        var path = $"removedroles_{id}.json";

        if (!File.Exists(path)) File.Create(path);

        var json = File.ReadAllText(path);
        var removedRoles = JsonConvert.DeserializeObject<Dictionary<ulong, ReadOnlyCollection<ulong>>>(json)
                           ?? new Dictionary<ulong, ReadOnlyCollection<ulong>>();

        RemovedRolesDictionary.Add(id, removedRoles);

        return removedRoles;
    }

    public static SocketGuild FindGuild(ulong channel) {
        if (GuildCache.ContainsKey(channel)) return GuildCache[channel];
        foreach (var guild in Client.Guilds) {
            if (guild.Channels.All(x => x.Id != channel)) continue;
            GuildCache.Add(channel, guild);
            return guild;
        }

        throw new Exception("Could not find guild by channel!");
    }
}
