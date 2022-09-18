namespace Boyfriend.Commands;

public sealed class SelfBanCommand : ICommand {
    public string[] Aliases { get; } = { "grantoverseer", "grant", "overseer", "voooo", "overseergrant", "special" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        await BanCommand.BanUser(cmd, cmd.Context.User, TimeSpan.FromMilliseconds(-1), "");
    }
}
