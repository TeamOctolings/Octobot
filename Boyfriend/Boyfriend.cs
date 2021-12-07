using Discord;
using Discord.WebSocket;

namespace Boyfriend;
    public class Boyfriend {

        public static void Main(string[] args)
            => new Boyfriend().MainAsync().GetAwaiter().GetResult();

        public static readonly DiscordSocketClient Client = new();

        private async Task MainAsync() {
            Client.Log += Log;
            var token = File.ReadAllText("token.txt").Trim();

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            await new CommandHandler().InstallCommandsAsync();

            await Task.Delay(-1);
        }

        private static Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }