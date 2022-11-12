using System.Collections.ObjectModel;
using System.Text;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Boyfriend;

public static class Boyfriend {
    public static readonly StringBuilder StringBuilder = new();

    private static readonly DiscordSocketConfig Config = new() {
        MessageCacheSize = 250,
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
        AlwaysDownloadUsers = true,
        AlwaysResolveStickers = false,
        AlwaysDownloadDefaultStickers = false,
        LargeThreshold = 500
    };

    private static readonly List<Tuple<Game, TimeSpan>> ActivityList = new() {
        Tuple.Create(new Game("UNDEAD CORPORATION - Everything will freeze", ActivityType.Listening),
            new TimeSpan(0, 3, 18)),
        Tuple.Create(new Game("Xi - Blue Zenith", ActivityType.Listening), new TimeSpan(0, 4, 16)),
        Tuple.Create(new Game("Kurokotei - Scattered Faith", ActivityType.Listening), new TimeSpan(0, 8, 21)),
        Tuple.Create(new Game("Splatoon 3 - Candy-Coated Rocks", ActivityType.Listening), new TimeSpan(0, 2, 39)),
        Tuple.Create(new Game("RetroSpecter - Genocide", ActivityType.Listening), new TimeSpan(0, 5, 52)),
        Tuple.Create(new Game("Dimrain47 - At the Speed of Light", ActivityType.Listening), new TimeSpan(0, 4, 10))
    };

    public static readonly DiscordSocketClient Client = new(Config);

    private static readonly Dictionary<ulong, Dictionary<string, string>> GuildConfigDictionary = new();

    private static readonly Dictionary<ulong, Dictionary<ulong, ReadOnlyCollection<ulong>>> RemovedRolesDictionary =
        new();

    public static readonly Dictionary<string, string> DefaultConfig = new() {
        { "Prefix", "!" },
        { "Lang", "en" },
        { "ReceiveStartupMessages", "false" },
        { "WelcomeMessage", "default" },
        { "SendWelcomeMessages", "true" },
        { "BotLogChannel", "0" },
        { "StarterRole", "0" },
        { "MuteRole", "0" },
        { "RemoveRolesOnMute", "false" },
        { "FrowningFace", "true" },
        { "EventStartedReceivers", "interested,role" },
        { "EventNotificationRole", "0" },
        { "EventNotificationChannel", "0" },
        { "EventEarlyNotificationOffset", "0" }
    };

    public static void Main() {
        Init().GetAwaiter().GetResult();
    }

    private static async Task Init() {
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        EventHandler.InitEvents();

        while (true) {
            foreach (var activity in ActivityList) {
                await Client.SetActivityAsync(activity.Item1);
                await Task.Delay(activity.Item2);
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static Task Log(LogMessage msg) {
        switch (msg.Severity) {
            case LogSeverity.Critical:
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine(msg.ToString());
                break;
            case LogSeverity.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(msg.ToString());
                break;
            case LogSeverity.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(msg.ToString());
                break;
            case LogSeverity.Info:
                Console.WriteLine(msg.ToString());
                break;

            case LogSeverity.Verbose:
            case LogSeverity.Debug:
            default: return Task.CompletedTask;
        }

        Console.ResetColor();
        return Task.CompletedTask;
    }

    public static async Task WriteGuildConfigAsync(ulong id) {
        var json = JsonConvert.SerializeObject(GuildConfigDictionary[id], Formatting.Indented);
        var removedRoles = JsonConvert.SerializeObject(RemovedRolesDictionary[id], Formatting.Indented);

        await File.WriteAllTextAsync($"config_{id}.json", json);
        await File.WriteAllTextAsync($"removedroles_{id}.json", removedRoles);
    }

    public static Dictionary<string, string> GetGuildConfig(ulong id) {
        if (GuildConfigDictionary.TryGetValue(id, out var cfg)) return cfg;

        var path = $"config_{id}.json";

        if (!File.Exists(path)) File.Create(path).Dispose();

        var json = File.ReadAllText(path);
        var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();

        if (config.Keys.Count < DefaultConfig.Keys.Count) {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // Conversion will result in a lot of memory allocations
            foreach (var key in DefaultConfig.Keys)
                if (!config.ContainsKey(key))
                    config.Add(key, DefaultConfig[key]);
        } else if (config.Keys.Count > DefaultConfig.Keys.Count) {
            foreach (var key in config.Keys.Where(key => !DefaultConfig.ContainsKey(key))) config.Remove(key);
        }

        GuildConfigDictionary.Add(id, config);

        return config;
    }

    public static Dictionary<ulong, ReadOnlyCollection<ulong>> GetRemovedRoles(ulong id) {
        if (RemovedRolesDictionary.TryGetValue(id, out var dict)) return dict;
        var path = $"removedroles_{id}.json";

        if (!File.Exists(path)) File.Create(path).Dispose();

        var json = File.ReadAllText(path);
        var removedRoles = JsonConvert.DeserializeObject<Dictionary<ulong, ReadOnlyCollection<ulong>>>(json)
                           ?? new Dictionary<ulong, ReadOnlyCollection<ulong>>();

        RemovedRolesDictionary.Add(id, removedRoles);

        return removedRoles;
    }
}
