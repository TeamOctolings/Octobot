using Discord;
using Discord.WebSocket;

namespace Boyfriend;
    public static class Boyfriend {

        public static void Main()
            => Init().GetAwaiter().GetResult();

        private static readonly DiscordSocketConfig Config = new() {
            MessageCacheSize = 250,
            GatewayIntents = GatewayIntents.All
        };
        public static readonly DiscordSocketClient Client = new(Config);

        private static async Task Init() {
            Client.Log += Log;
            var token = (await File.ReadAllTextAsync("token.txt")).Trim();

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();
            await Client.SetActivityAsync(new Game("Retrospecter - Electrospasm", ActivityType.Listening));

            await new EventHandler().InitEvents();

            await Task.Delay(-1);
        }

        private static Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }