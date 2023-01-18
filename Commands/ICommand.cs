namespace Boyfriend.Commands;

public interface ICommand {
    public string[] Aliases { get; }

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs);
}
