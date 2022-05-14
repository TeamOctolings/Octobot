using Discord.Commands;

namespace Boyfriend.Commands;

public class PingCommand : Command {
    public override string[] Aliases { get; } = {"ping", "latency", "pong", "пинг", "задержка", "понг"};
    public override int ArgsLengthRequired => 0;

    public override Task Run(SocketCommandContext context, string[] args) {
        var builder = Boyfriend.StringBuilder;

        builder.Append(Utils.GetBeep()).Append(Boyfriend.Client.Latency).Append(Messages.Milliseconds);

        Output(ref builder);
        builder.Clear();

        return Task.CompletedTask;
    }
}