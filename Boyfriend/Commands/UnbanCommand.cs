using Discord;

namespace Boyfriend.Commands;

public sealed class UnbanCommand : ICommand {
    public string[] Aliases { get; } = { "unban", "разбан" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (!cmd.HasPermission(GuildPermission.BanMembers)) return;

        var id = cmd.GetBan(args, 0);
        if (id is null) return;
        var reason = cmd.GetRemaining(args, 1, "UnbanReason");
        if (reason is not null) await UnbanUserAsync(cmd, id.Value, reason);
    }

    public static async Task UnbanUserAsync(CommandProcessor cmd, ulong id, string reason) {
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        await cmd.Context.Guild.RemoveBanAsync(id, requestOptions);

        var feedback = string.Format(Messages.FeedbackUserUnbanned, $"<@{id.ToString()}>", Utils.Wrap(reason));
        cmd.Reply(feedback);
        cmd.Audit(feedback);
    }
}
