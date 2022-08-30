using Discord;

namespace Boyfriend.Commands;

public class UnbanCommand : Command {
    public override string[] Aliases { get; } = { "unban", "разбан" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        if (!cmd.HasPermission(GuildPermission.BanMembers)) return;

        var id = cmd.GetBan(args, 0);
        if (id == null) return;
        var reason = cmd.GetRemaining(args, 1, "UnbanReason");
        if (reason == null) return;

        await UnbanUser(cmd, id.Value, reason);
    }

    public static async Task UnbanUser(CommandProcessor cmd, ulong id, string reason) {
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        await cmd.Context.Guild.RemoveBanAsync(id, requestOptions);

        var feedback = string.Format(Messages.FeedbackUserUnbanned, $"<@{id.ToString()}>", Utils.Wrap(reason));
        cmd.Reply(feedback);
        cmd.Audit(feedback);
    }
}