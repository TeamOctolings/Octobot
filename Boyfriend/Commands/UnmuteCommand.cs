using Boyfriend.Data;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class UnmuteCommand : ICommand {
    public string[] Aliases { get; } = { "unmute", "размут" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (!cmd.HasPermission(GuildPermission.ModerateMembers)) return;

        var toUnmute = cmd.GetMember(args, cleanArgs, 0, "ToUnmute");
        if (toUnmute is null) return;
        var reason = cmd.GetRemaining(args, 1, "UnmuteReason");
        if (reason is not null && cmd.CanInteractWith(toUnmute, "Unmute"))
            await UnmuteMemberAsync(cmd, toUnmute, reason);
    }

    public static async Task UnmuteMemberAsync(CommandProcessor cmd, SocketGuildUser toUnmute,
        string reason) {
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        var role = GuildData.FromSocketGuild(cmd.Context.Guild).MuteRole;

        if (role is not null && toUnmute.Roles.Contains(role)) {
            // TODO: Return roles
        } else {
            if (toUnmute.TimedOutUntil is null || toUnmute.TimedOutUntil.Value.ToUnixTimeSeconds() <
                DateTimeOffset.Now.ToUnixTimeSeconds()) {
                cmd.Reply(Messages.MemberNotMuted, ReplyEmojis.Error);
                return;
            }

            await toUnmute.RemoveTimeOutAsync();
        }

        var feedback = string.Format(Messages.FeedbackMemberUnmuted, toUnmute.Mention, Utils.Wrap(reason));
        cmd.Reply(feedback, ReplyEmojis.Unmuted);
        cmd.Audit(feedback);
    }
}
