using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class MuteCommand : ICommand {
    public string[] Aliases { get; } = { "mute", "timeout", "заглушить", "мут" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var toMute = cmd.GetMember(args, cleanArgs, 0, "ToMute");
        if (toMute is null) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "MuteReason");
        if (reason is null) return;
        var role = Utils.GetMuteRole(cmd.Context.Guild);

        if ((role is not null && toMute.Roles.Contains(role))
            || (toMute.TimedOutUntil is not null
                && toMute.TimedOutUntil.Value.ToUnixTimeSeconds()
                > DateTimeOffset.Now.ToUnixTimeSeconds())) {
            cmd.Reply(Messages.MemberAlreadyMuted, ReplyEmojis.Error);
            return;
        }

        var rolesRemoved = Boyfriend.GetRemovedRoles(cmd.Context.Guild.Id);

        if (rolesRemoved.TryGetValue(toMute.Id, out var mutedRemovedRoles)) {
            foreach (var roleId in mutedRemovedRoles) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            cmd.ConfigWriteScheduled = true;
            cmd.Reply(Messages.RolesReturned, ReplyEmojis.Warning);
        }

        if (cmd.HasPermission(GuildPermission.ModerateMembers) && cmd.CanInteractWith(toMute, "Mute"))
            await MuteMemberAsync(cmd, toMute, duration, reason);
    }

    private static async Task MuteMemberAsync(CommandProcessor cmd, SocketGuildUser toMute,
        TimeSpan duration, string reason) {
        var guild = cmd.Context.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        var role = Utils.GetMuteRole(guild);
        var hasDuration = duration.TotalSeconds > 0;

        if (role is not null) {
            if (config["RemoveRolesOnMute"] is "true") {
                var rolesRemoved = new List<ulong>();
                foreach (var userRole in toMute.Roles)
                    try {
                        if (userRole == guild.EveryoneRole || userRole == role) continue;
                        await toMute.RemoveRoleAsync(role);
                        rolesRemoved.Add(userRole.Id);
                    } catch (HttpException e) {
                        cmd.Reply(string.Format(Messages.RoleRemovalFailed, $"<@&{userRole}>", Utils.Wrap(e.Reason)),
                            ReplyEmojis.Warning);
                    }

                Boyfriend.GetRemovedRoles(guild.Id).Add(toMute.Id, rolesRemoved.AsReadOnly());
                cmd.ConfigWriteScheduled = true;
            }

            await toMute.AddRoleAsync(role, requestOptions);

            if (hasDuration)
                await Task.FromResult(Utils.DelayedUnmuteAsync(cmd, toMute, Messages.PunishmentExpired, duration));
        } else {
            if (!hasDuration || duration.TotalDays > 28) {
                cmd.Reply(Messages.DurationRequiredForTimeOuts, ReplyEmojis.Error);
                return;
            }

            if (toMute.IsBot) {
                cmd.Reply(Messages.CannotTimeOutBot, ReplyEmojis.Error);
                return;
            }

            await toMute.SetTimeOutAsync(duration, requestOptions);
        }

        var feedback = string.Format(Messages.FeedbackMemberMuted, toMute.Mention,
            Utils.GetHumanizedTimeOffset(duration),
            Utils.Wrap(reason));
        cmd.Reply(feedback, ReplyEmojis.Muted);
        cmd.Audit(feedback);
    }
}
