using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class KickCommand : Command {
    public override string[] Aliases { get; } = { "kick", "кик", "выгнать" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        var toKick = cmd.GetMember(args, 0, "ToKick");
        if (toKick == null || !cmd.HasPermission(GuildPermission.KickMembers)) return;

        if (!cmd.CanInteractWith(toKick, "Kick")) return;

        await KickMember(cmd, toKick, cmd.GetRemaining(args, 1, "KickReason"));
    }

    private static async Task KickMember(CommandProcessor cmd, SocketGuildUser toKick, string? reason) {
        if (reason == null) return;
        var guildKickMessage = $"({cmd.Context.User}) {reason}";

        await Utils.SendDirectMessage(toKick,
            string.Format(Messages.YouWereKicked, cmd.Context.User.Mention, cmd.Context.Guild.Name,
                Utils.Wrap(reason)));

        await toKick.KickAsync(guildKickMessage);
        var format = string.Format(Messages.FeedbackMemberKicked, toKick.Mention, Utils.Wrap(reason));
        cmd.Reply(format, ":police_car: ");
        cmd.Audit(format);
    }
}