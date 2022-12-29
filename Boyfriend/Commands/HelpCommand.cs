using Boyfriend.Data;
using Humanizer;

namespace Boyfriend.Commands;

public sealed class HelpCommand : ICommand {
    public string[] Aliases { get; } = { "help", "помощь", "справка" };

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var prefix = GuildData.FromSocketGuild(cmd.Context.Guild).Preferences["Prefix"];
        var toSend = Boyfriend.StringBuilder.Append(Messages.CommandHelp);

        foreach (var command in CommandProcessor.Commands)
            toSend.Append(
                $"\n`{prefix}{command.Aliases[0]}`: {Utils.GetMessage($"CommandDescription{command.Aliases[0].Titleize()}")}");
        cmd.Reply(toSend.ToString(), ReplyEmojis.Help);
        toSend.Clear();

        return Task.CompletedTask;
    }
}
