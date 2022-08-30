using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class UnmuteCommand : Command {
    public override string[] Aliases { get; } = { "unmute", "размут" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        if (!cmd.HasPermission(GuildPermission.ModerateMembers)) return;

        var toUnmute = cmd.GetMember(args, 0, "ToUnmute");
        var reason = cmd.GetRemaining(args, 1, "UnmuteReason");
        if (toUnmute == null || reason == null || !cmd.CanInteractWith(toUnmute, "Unmute")) return;
        await UnmuteMember(cmd, toUnmute, reason);
    }

    public static async Task UnmuteMember(CommandProcessor cmd, SocketGuildUser toUnmute,
        string reason) {
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        var role = Utils.GetMuteRole(cmd.Context.Guild);

        if (role != null && toUnmute.Roles.Contains(role)) {
            var rolesRemoved = Boyfriend.GetRemovedRoles(cmd.Context.Guild.Id);

            if (rolesRemoved.ContainsKey(toUnmute.Id)) {
                await toUnmute.AddRolesAsync(rolesRemoved[toUnmute.Id]);
                rolesRemoved.Remove(toUnmute.Id);
                cmd.ConfigWriteScheduled = true;
            }

            await toUnmute.RemoveRoleAsync(role, requestOptions);
        } else {
            if (toUnmute.TimedOutUntil == null || toUnmute.TimedOutUntil.Value.ToUnixTimeMilliseconds() <
                DateTimeOffset.Now.ToUnixTimeMilliseconds()) {
                cmd.Reply(Messages.MemberNotMuted, ":x: ");
                return;
            }

            await toUnmute.RemoveTimeOutAsync();
        }

        var feedback = string.Format(Messages.FeedbackMemberUnmuted, toUnmute.Mention, Utils.Wrap(reason));
        cmd.Reply(feedback, ":loud_sound: ");
        cmd.Audit(feedback);
    }
}