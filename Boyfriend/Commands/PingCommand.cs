namespace Boyfriend.Commands;

public sealed class PingCommand : ICommand {
    public string[] Aliases { get; } = { "ping", "latency", "pong", "пинг", "задержка", "понг" };

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var builder = Boyfriend.StringBuilder;

        builder.Append(Utils.GetBeep())
            .Append(Math.Round(Math.Abs(DateTimeOffset.Now.Subtract(cmd.Context.Message.Timestamp).TotalMilliseconds)))
            .Append(Messages.Milliseconds);

        cmd.Reply(builder.ToString(), ReplyEmojis.Ping);
        builder.Clear();

        return Task.CompletedTask;
    }
}
