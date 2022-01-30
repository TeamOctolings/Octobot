using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class PingCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        await context.Channel.SendMessageAsync($"{Utils.GetBeep(Boyfriend.GetGuildConfig(context.Guild).Lang!)}" +
                                               $"{Boyfriend.Client.Latency}{Messages.Milliseconds}");
    }

    public override List<string> GetAliases() {
        return new List<string> {"ping", "пинг", "задержка"};
    }

    public override int GetArgumentsAmountRequired() {
        return 0;
    }

    public override string GetSummary() {
        return "Измеряет время обработки REST-запроса";
    }
}
