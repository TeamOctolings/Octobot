using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class MuteCommand : Command {
    public override string[] Aliases { get; } = {"mute", "timeout", "заглушить", "мут"};
    public override int ArgsLengthRequired => 2;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var toMute = Utils.ParseMember(context.Guild, args[0]);
        var reason = Utils.JoinString(ref args, 2);

        var duration = Utils.GetTimeSpan(ref args[1]) ?? TimeSpan.FromMilliseconds(-1);
        if (duration.TotalSeconds < 0) {
            Warn(Messages.DurationParseFailed);
            reason = Utils.JoinString(ref args, 1);
        }

        if (toMute == null) {
            Error(Messages.UserNotInGuild, false);
            return;
        }

        var guild = context.Guild;
        var role = Utils.GetMuteRole(ref guild);

        if (role != null) {
            var hasMuteRole = false;
            foreach (var x in toMute.Roles) {
                if (x != role) continue;
                hasMuteRole = true;
                break;
            }

            if (hasMuteRole || (toMute.TimedOutUntil != null && toMute.TimedOutUntil.Value.ToUnixTimeMilliseconds() >
                    DateTimeOffset.Now.ToUnixTimeMilliseconds())) {
                Error(Messages.MemberAlreadyMuted, false);
                return;
            }
        }

        var rolesRemoved = Boyfriend.GetRemovedRoles(context.Guild.Id);

        if (rolesRemoved.ContainsKey(toMute.Id)) {
            foreach (var roleId in rolesRemoved[toMute.Id]) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            CommandHandler.ConfigWriteScheduled = true;
            Warn(Messages.RolesReturned);
        }

        var author = (SocketGuildUser) context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.ModerateMembers);
        if (permissionCheckResponse != "") {
            Error(permissionCheckResponse, true);
            return;
        }

        var interactionCheckResponse = CommandHandler.CanInteract(ref author, ref toMute);
        if (interactionCheckResponse != "") {
            Error(interactionCheckResponse, true);
            return;
        }

        await MuteMember(guild, author, toMute, duration, reason);

        Success(
            string.Format(Messages.FeedbackMemberMuted, toMute.Mention, Utils.GetHumanizedTimeOffset(ref duration),
                Utils.WrapInline(reason)), author.Mention, true);
    }

    private static async Task MuteMember(SocketGuild guild, SocketUser author, SocketGuildUser toMute,
        TimeSpan duration, string reason) {
        var config = Boyfriend.GetGuildConfig(guild.Id);
        var requestOptions = Utils.GetRequestOptions($"({author}) {reason}");
        var role = Utils.GetMuteRole(ref guild);
        var hasDuration = duration.TotalSeconds > 0;

        if (role != null) {
            if (config["RemoveRolesOnMute"] == "true") {
                var rolesRemoved = new List<ulong>();
                foreach (var userRole in toMute.Roles)
                    try {
                        if (userRole == guild.EveryoneRole || userRole == role) continue;
                        await toMute.RemoveRoleAsync(role);
                        rolesRemoved.Add(userRole.Id);
                    } catch (HttpException e) {
                        Warn(string.Format(Messages.RoleRemovalFailed, $"<@&{userRole}>", Utils.WrapInline(e.Reason)));
                    }

                Boyfriend.GetRemovedRoles(guild.Id).Add(toMute.Id, rolesRemoved.AsReadOnly());
                CommandHandler.ConfigWriteScheduled = true;

                if (hasDuration) {
                    async void DelayUnmute() {
                        await Task.Delay(duration);
                        await UnmuteCommand.UnmuteMember(guild, guild.CurrentUser, toMute, Messages.PunishmentExpired);
                    }

                    var task = new Task(DelayUnmute);
                    task.Start();
                }
            }

            await toMute.AddRoleAsync(role, requestOptions);
        } else {
            if (!hasDuration) {
                Error(Messages.DurationRequiredForTimeOuts, false);
                return;
            }
            if (toMute.IsBot) {
                Error(Messages.CannotTimeOutBot, false);
                return;
            }

            await toMute.SetTimeOutAsync(duration, requestOptions);
        }
    }
}