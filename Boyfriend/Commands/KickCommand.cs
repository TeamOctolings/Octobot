using Boyfriend.Data;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class KickCommand : ICommand {
    public string[] Aliases { get; } = { "kick", "кик", "выгнать" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var toKick = cmd.GetMember(args, cleanArgs, 0, "ToKick");
        if (toKick is null || !cmd.HasPermission(GuildPermission.KickMembers)) return;

        if (cmd.CanInteractWith(toKick, "Kick"))
            await KickMemberAsync(cmd, toKick, cmd.GetRemaining(args, 1, "KickReason"));
    }

    private static async Task KickMemberAsync(CommandProcessor cmd, SocketGuildUser toKick, string? reason) {
        if (reason is null) return;
        var guildKickMessage = $"({cmd.Context.User}) {reason}";

        await Utils.SendDirectMessage(toKick,
            string.Format(Messages.YouWereKicked, cmd.Context.User.Mention, cmd.Context.Guild.Name,
                Utils.Wrap(reason)));

        GuildData.FromSocketGuild(cmd.Context.Guild).MemberData[toKick.Id].Roles.Clear();

        await toKick.KickAsync(guildKickMessage);
        var format = string.Format(Messages.FeedbackMemberKicked, toKick.Mention, Utils.Wrap(reason));
        cmd.Reply(format, ReplyEmojis.Kicked);
        cmd.Audit(format);
    }
}
