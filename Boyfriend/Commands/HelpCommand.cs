using Humanizer;

namespace Boyfriend.Commands;

public class HelpCommand : Command {
    public override string[] Aliases { get; } = { "help", "помощь", "справка" };

    public override Task Run(CommandProcessor cmd, string[] args) {
        var prefix = Boyfriend.GetGuildConfig(cmd.Context.Guild.Id)["Prefix"];
        var toSend = Boyfriend.StringBuilder.Append(Messages.CommandHelp);

        foreach (var command in CommandProcessor.Commands)
            toSend.Append(
                $"\n`{prefix}{command.Aliases[0]}`: {Utils.GetMessage($"CommandDescription{command.Aliases[0].Titleize()}")}");
        cmd.Reply(toSend.ToString(), ":page_facing_up: ");
        toSend.Clear();

        return Task.CompletedTask;
    }
}