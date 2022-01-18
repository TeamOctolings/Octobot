using System.Globalization;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

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
        Client.Log += Log;
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();
        await Client.SetActivityAsync(new Game("Retrospecter - Chiller", ActivityType.Listening));

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
            var openStream = !File.Exists(path) ? File.Create(path) : File.OpenRead(path);

            GuildConfig config;
            try {
                config = await JsonSerializer.DeserializeAsync<GuildConfig>(openStream) ?? throw new Exception();
            } catch (JsonException) {
                Messages.Culture = new CultureInfo("ru");
                config = new GuildConfig(guild.Id, "ru", "!", false, true,
                    true, Messages.DefaultWelcomeMessage, 0, 0, 0, 0);
            }
            GuildConfigDictionary.Add(guild.Id, config);
        }
    }

    public static GuildConfig GetGuildConfig(IGuild guild) {
        Messages.Culture = new CultureInfo("ru");
        var toReturn = GuildConfigDictionary.ContainsKey(guild.Id) ? GuildConfigDictionary[guild.Id]
            : new GuildConfig(guild.Id, "ru", "!", false, true, true, Messages.DefaultWelcomeMessage, 0, 0, 0, 0);

        if (toReturn.Id != guild.Id) throw new Exception();
        return toReturn;
    }

    public static IGuild FindGuild(IMessageChannel channel) {
        foreach (var guild in Client.Guilds)
            if (guild.Channels.Any(x => x == channel))
                return guild;

        throw new Exception(Messages.CouldntFindGuildByChannel);
    }
}