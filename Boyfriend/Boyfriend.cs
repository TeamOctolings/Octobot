using System.Text;
using Discord;
using Discord.WebSocket;

namespace Boyfriend;

public static class Boyfriend {
    public static readonly StringBuilder StringBuilder = new();

    private static readonly DiscordSocketConfig Config = new() {
        MessageCacheSize = 250,
        GatewayIntents
            = (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers) &
              ~GatewayIntents.GuildInvites,
        AlwaysDownloadUsers = true,
        AlwaysResolveStickers = false,
        AlwaysDownloadDefaultStickers = false,
        LargeThreshold = 500
    };

    private static readonly List<Tuple<Game, TimeSpan>> ActivityList = new() {
        Tuple.Create(new Game("Masayoshi Minoshima (ft. nomico) - Bad Apple!!", ActivityType.Listening),
            new TimeSpan(0, 3, 40)),
        Tuple.Create(new Game("Xi - Blue Zenith", ActivityType.Listening), new TimeSpan(0, 4, 16)),
        Tuple.Create(new Game("Kurokotei - Scattered Faith", ActivityType.Listening), new TimeSpan(0, 8, 21)),
        Tuple.Create(new Game("Splatoon 3 - Candy-Coated Rocks", ActivityType.Listening), new TimeSpan(0, 2, 39)),
        Tuple.Create(new Game("RetroSpecter - Genocide", ActivityType.Listening), new TimeSpan(0, 5, 52)),
        Tuple.Create(new Game("beatMARIO - Night of Knights", ActivityType.Listening), new TimeSpan(0, 4, 10))
    };

    public static readonly DiscordSocketClient Client = new(Config);

    public static void Main() {
        Init().GetAwaiter().GetResult();
    }

    private static async Task Init() {
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        EventHandler.InitEvents();

        while (ActivityList.Count > 0)
            foreach (var activity in ActivityList) {
                await Client.SetActivityAsync(activity.Item1);
                await Task.Delay(activity.Item2);
            }
    }

    public static Task Log(LogMessage msg) {
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

    /*public static Dictionary<ulong, ReadOnlyCollection<ulong>> GetRemovedRoles(ulong id) {
        if (RemovedRolesDictionary.TryGetValue(id, out var dict)) return dict;
        var path = $"removedroles_{id}.json";

        if (!File.Exists(path)) File.Create(path).Dispose();

        var json = File.ReadAllText(path);
        var removedRoles = JsonConvert.DeserializeObject<Dictionary<ulong, ReadOnlyCollection<ulong>>>(json)
                           ?? new Dictionary<ulong, ReadOnlyCollection<ulong>>();

        RemovedRolesDictionary.Add(id, removedRoles);

        return removedRoles;
    }*/
}
