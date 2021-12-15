using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class HelpModule : ModuleBase<SocketCommandContext> {

    [Command("help")]
    [Summary("Показывает эту справку")]
    [Alias("помощь", "справка")]
    public Task Run() {
        var nl = Environment.NewLine;
        var toSend = $"Справка по командам:{nl}";
        var prefix = Boyfriend.GetGuildConfig(Context.Guild).Prefix;
        foreach (var command in EventHandler.Commands.Commands) {
            var aliases = command.Aliases.Aggregate("", (current, alias) =>
                current + (current == "" ? "" : $", {prefix}") + alias);
            toSend += $"`{prefix}{aliases}`: {command.Summary}{nl}";
        }

        Context.Channel.SendMessageAsync(toSend);
        return Task.CompletedTask;
    }
}