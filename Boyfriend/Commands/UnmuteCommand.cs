using Boyfriend.Data;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class UnmuteCommand : ICommand {
    public string[] Aliases { get; } = { "unmute", "размут" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (!cmd.HasPermission(GuildPermission.ModerateMembers)) return;

        var toUnmute = cmd.GetMember(args, 0);
        if (toUnmute is null) return;
        var reason = cmd.GetRemaining(args, 1, "UnmuteReason");
        if (reason is not null && cmd.CanInteractWith(toUnmute, "Unmute"))
            await UnmuteMemberAsync(cmd, toUnmute, reason);
    }

    private static async Task UnmuteMemberAsync(CommandProcessor cmd, SocketGuildUser toUnmute,
        string reason) {
        var isMuted = await Utils.UnmuteMemberAsync(GuildData.Get(cmd.Context.Guild), cmd.Context.User.ToString(),
            toUnmute, reason);

        if (!isMuted) {
            cmd.Reply(Messages.MemberNotMuted, ReplyEmojis.Error);
            return;
        }

        cmd.ConfigWriteScheduled = true;

        var feedback = string.Format(Messages.FeedbackMemberUnmuted, toUnmute.Mention, Utils.Wrap(reason));
        cmd.Reply(feedback, ReplyEmojis.Unmuted);
        cmd.Audit(feedback);
    }
}
