using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class UnmuteCommand : Command {
    public override string[] Aliases { get; } = { "unmute", "размут" };
    public override int ArgsLengthRequired => 2;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var author = (SocketGuildUser)context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.ModerateMembers);
        if (permissionCheckResponse != "") {
            Error(permissionCheckResponse, true);
            return;
        }

        var toUnmute = Utils.ParseMember(context.Guild, args[0]);

        if (toUnmute == null) {
            Error(Messages.UserDoesntExist, false);
            return;
        }

        var interactionCheckResponse = CommandHandler.CanInteract(ref author, ref toUnmute);
        if (interactionCheckResponse != "") {
            Error(interactionCheckResponse, true);
            return;
        }

        var reason = Utils.JoinString(ref args, 1);
        await UnmuteMember(context.Guild, author, toUnmute, reason);
    }

    public static async Task UnmuteMember(SocketGuild guild, SocketGuildUser author, SocketGuildUser toUnmute,
        string reason) {
        var requestOptions = Utils.GetRequestOptions($"({author}) {reason}");
        var role = Utils.GetMuteRole(ref guild);

        if (role != null) {
            var rolesRemoved = Boyfriend.GetRemovedRoles(guild.Id);

            if (rolesRemoved.ContainsKey(toUnmute.Id)) {
                await toUnmute.AddRolesAsync(rolesRemoved[toUnmute.Id]);
                rolesRemoved.Remove(toUnmute.Id);
                CommandHandler.ConfigWriteScheduled = true;
            }

            if (toUnmute.Roles.Contains(role)) { await toUnmute.RemoveRoleAsync(role, requestOptions); } else {
                Error(Messages.MemberNotMuted, false);
                return;
            }
        } else {
            if (toUnmute.TimedOutUntil == null || toUnmute.TimedOutUntil.Value.ToUnixTimeMilliseconds() <
                DateTimeOffset.Now.ToUnixTimeMilliseconds()) {
                Error(Messages.MemberNotMuted, false);
                return;
            }

            await toUnmute.RemoveTimeOutAsync();
        }

        var feedback = string.Format(Messages.FeedbackMemberUnmuted, toUnmute.Mention, Utils.Wrap(reason));
        Success(feedback, author.Mention, false, false);
        await Utils.SendFeedback(feedback, guild.Id, author.Mention, true);
    }
}
