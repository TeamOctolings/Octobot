using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public abstract class Command {
    public abstract Task Run(SocketCommandContext context, string[] args);

    public abstract List<string> GetAliases();

    public abstract int GetArgumentsAmountRequired();

    public abstract string GetSummary();

    protected static async Task Warn(ISocketMessageChannel channel, string warning) {
        await Utils.SilentSendAsync(channel as ITextChannel, ":warning: " + warning);
    }
}