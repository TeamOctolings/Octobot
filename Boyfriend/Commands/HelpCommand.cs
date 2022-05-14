using Discord.Commands;
using Humanizer;

namespace Boyfriend.Commands;

public class HelpCommand : Command {
    public override string[] Aliases { get; } = {"help", "помощь", "справка"};
    public override int ArgsLengthRequired => 0;

    public override Task Run(SocketCommandContext context, string[] args) {
        var prefix = Boyfriend.GetGuildConfig(context.Guild.Id)["Prefix"];
        var toSend = Boyfriend.StringBuilder.Append(Messages.CommandHelp);

        foreach (var command in CommandHandler.Commands)
            toSend.Append(
                $"\n`{prefix}{command.Aliases[0]}`: {Utils.GetMessage($"CommandDescription{command.Aliases[0].Titleize()}")}");
        Output(ref toSend);
        toSend.Clear();

        return Task.CompletedTask;
    }
}