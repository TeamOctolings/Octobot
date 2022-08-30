namespace Boyfriend.Commands;

public class PingCommand : Command {
    public override string[] Aliases { get; } = { "ping", "latency", "pong", "пинг", "задержка", "понг" };

    public override Task Run(CommandProcessor cmd, string[] args) {
        var builder = Boyfriend.StringBuilder;

        builder.Append(Utils.GetBeep())
            .Append(Math.Abs(DateTimeOffset.Now.Subtract(cmd.Context.Message.Timestamp).TotalMilliseconds))
            .Append(Messages.Milliseconds);

        cmd.Reply(builder.ToString(), ":signal_strength: ");
        builder.Clear();

        return Task.CompletedTask;
    }
}