using Boyfriend.Data;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class MuteCommand : ICommand {
    public string[] Aliases { get; } = { "mute", "timeout", "заглушить", "мут" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var toMute = cmd.GetMember(args, 0, "ToMute");
        if (toMute is null) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "MuteReason");
        if (reason is null) return;
        var guildData = GuildData.Get(cmd.Context.Guild);
        var role = guildData.MuteRole;

        if ((role is not null && toMute.Roles.Contains(role))
            || (toMute.TimedOutUntil is not null
                && toMute.TimedOutUntil.Value
                > DateTimeOffset.Now)) {
            cmd.Reply(Messages.MemberAlreadyMuted, ReplyEmojis.Error);
            return;
        }

        if (cmd.HasPermission(GuildPermission.ModerateMembers) && cmd.CanInteractWith(toMute, "Mute"))
            await MuteMemberAsync(cmd, toMute, duration, guildData, reason);
    }

    private static async Task MuteMemberAsync(CommandProcessor cmd, SocketGuildUser toMute,
        TimeSpan duration, GuildData data, string reason) {
        var requestOptions = Utils.GetRequestOptions($"({cmd.Context.User}) {reason}");
        var role = data.MuteRole;
        var hasDuration = duration.TotalSeconds > 0;

        if (role is not null) {
            if (data.Preferences["RemoveRolesOnMute"] is "true")
                await toMute.RemoveRolesAsync(toMute.Roles, requestOptions);

            await toMute.AddRoleAsync(role, requestOptions);
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

        data.MemberData[toMute.Id].MutedUntil = DateTimeOffset.Now.Add(duration);
        cmd.ConfigWriteScheduled = true;

        var feedback = string.Format(Messages.FeedbackMemberMuted, toMute.Mention,
            Utils.GetHumanizedTimeSpan(duration),
            Utils.Wrap(reason));
        cmd.Reply(feedback, ReplyEmojis.Muted);
        cmd.Audit(feedback);
    }
}
