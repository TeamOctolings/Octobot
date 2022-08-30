using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class MuteCommand : Command {
    public override string[] Aliases { get; } = { "mute", "timeout", "заглушить", "мут" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        var toMute = cmd.GetMember(args, 0, "ToMute");
        if (toMute == null) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "MuteReason");
        if (reason == null) return;
        var role = Utils.GetMuteRole(cmd.Context.Guild);

        if (role != null) {
            if (toMute.Roles.Contains(role) || (toMute.TimedOutUntil != null &&
                                                toMute.TimedOutUntil.Value.ToUnixTimeMilliseconds() >
                                                DateTimeOffset.Now.ToUnixTimeMilliseconds())) {
                cmd.Reply(Messages.MemberAlreadyMuted, ":x: ");
                return;
            }
        }

        var rolesRemoved = Boyfriend.GetRemovedRoles(cmd.Context.Guild.Id);

        if (rolesRemoved.ContainsKey(toMute.Id)) {
            foreach (var roleId in rolesRemoved[toMute.Id]) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            cmd.ConfigWriteScheduled = true;
            cmd.Reply(Messages.RolesReturned, ":warning: ");
        }

        if (!cmd.HasPermission(GuildPermission.ModerateMembers) || !cmd.CanInteractWith(toMute, "Mute")) return;

        await MuteMember(cmd, toMute, duration, reason);
    }

    private static async Task MuteMember(CommandProcessor cmd, SocketGuildUser toMute,
        TimeSpan duration, string reason) {
        var guild = cmd.Context.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        var role = Utils.GetMuteRole(guild);
        var hasDuration = duration.TotalSeconds > 0;

        if (role != null) {
            if (config["RemoveRolesOnMute"] is "true") {
                var rolesRemoved = new List<ulong>();
                foreach (var userRole in toMute.Roles)
                    try {
                        if (userRole == guild.EveryoneRole || userRole == role) continue;
                        await toMute.RemoveRoleAsync(role);
                        rolesRemoved.Add(userRole.Id);
                    } catch (HttpException e) {
                        cmd.Reply(string.Format(Messages.RoleRemovalFailed, $"<@&{userRole}>", Utils.Wrap(e.Reason)),
                            ":warning: ");
                    }

                Boyfriend.GetRemovedRoles(guild.Id).Add(toMute.Id, rolesRemoved.AsReadOnly());
                cmd.ConfigWriteScheduled = true;
            }

            await toMute.AddRoleAsync(role, requestOptions);

            if (hasDuration) {
                var _ = async () => {
                    await Task.Delay(duration);
                    await UnmuteCommand.UnmuteMember(cmd, toMute, Messages.PunishmentExpired);
                };
            }
        } else {
            if (!hasDuration || duration.TotalDays > 28) {
                cmd.Reply(Messages.DurationRequiredForTimeOuts, ":x: ");
                return;
            }

            if (toMute.IsBot) {
                cmd.Reply(Messages.CannotTimeOutBot, ":x: ");
                return;
            }

            await toMute.SetTimeOutAsync(duration, requestOptions);
        }

        var feedback = string.Format(Messages.FeedbackMemberMuted, toMute.Mention,
            Utils.GetHumanizedTimeOffset(duration),
            Utils.Wrap(reason));
        cmd.Reply(feedback, ":mute: ");
        cmd.Audit(feedback);
    }
}