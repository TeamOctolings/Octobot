using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class HelpCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var nl = Environment.NewLine;
        var toSend = string.Format(Messages.CommandHelp, nl);
        var prefix = Boyfriend.GetGuildConfig(context.Guild).Prefix;
        toSend = CommandHandler.Commands.Aggregate(toSend,
            (current, command) => current + $"`{prefix}{command.GetAliases()[0]}`: {command.GetSummary()}{nl}");

        await context.Channel.SendMessageAsync(toSend);
    }

    public override List<string> GetAliases() {
        return new List<string> {"help", "помощь", "справка"};
    }

    public override int GetArgumentsAmountRequired() {
        return 0;
    }

    public override string GetSummary() {
        return "Показывает эту справку";
    }
}