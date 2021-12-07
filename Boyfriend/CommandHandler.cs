using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend;

public class CommandHandler {
    private readonly DiscordSocketClient _client = Boyfriend.Client;
    private readonly CommandService _commands = new CommandService();

    public async Task InstallCommandsAsync() {
        _client.MessageReceived += HandleCommandAsync;
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    }

    private async Task HandleCommandAsync(SocketMessage messageParam) {
        if (messageParam is not SocketUserMessage message) return;
        var argPos = 0;

        if (!(message.HasCharPrefix('!', ref argPos) ||
              message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;

        var context = new SocketCommandContext(_client, message);

        await _commands.ExecuteAsync(context, argPos, null);
    }
}