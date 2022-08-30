namespace Boyfriend.Commands;

public abstract class Command {
    public abstract string[] Aliases { get; }

    public abstract Task Run(CommandProcessor cmd, string[] args);
}