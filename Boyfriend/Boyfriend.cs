using System.Globalization;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Boyfriend;

public static class Boyfriend {

    private static readonly DiscordSocketConfig Config = new() {
        MessageCacheSize = 250,
        GatewayIntents = GatewayIntents.All
    };

    public static readonly DiscordSocketClient Client = new(Config);

    private static readonly Dictionary<ulong, GuildConfig> GuildConfigDictionary = new();

    public static void Main() {
        Init().GetAwaiter().GetResult();
    }

    private static async Task Init() {
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();
        await Client.SetActivityAsync(new Game("Retrospecter - Expurgation", ActivityType.Listening));

        new EventHandler().InitEvents();

        await Task.Delay(-1);
    }

    private static Task Log(LogMessage msg) {
        Console.WriteLine(msg.ToString());

        return Task.CompletedTask;
    }

    public static async Task SetupGuildConfigs() {
        foreach (var guild in Client.Guilds) {
            var path = "config_" + guild.Id + ".json";
            if (!File.Exists(path)) File.Create(path);

            var config = JsonConvert.DeserializeObject<GuildConfig>(await File.ReadAllTextAsync(path));
            if (config == null) {
                Messages.Culture = new CultureInfo("ru");
                config = new GuildConfig(guild.Id);
            }
            config.Validate();

            GuildConfigDictionary.Add(config.Id.GetValueOrDefault(0), config);
        }
    }

    public static GuildConfig GetGuildConfig(IGuild guild) {
        Messages.Culture = new CultureInfo("ru");

        var config = GuildConfigDictionary.ContainsKey(guild.Id) ? GuildConfigDictionary[guild.Id]
            : new GuildConfig(guild.Id);
        config.Validate();

        return config;
    }

    public static IGuild FindGuild(IMessageChannel channel) {
        foreach (var guild in Client.Guilds)
            if (guild.Channels.Any(x => x == channel))
                return guild;

        throw new Exception(Messages.CouldntFindGuildByChannel);
    }
}