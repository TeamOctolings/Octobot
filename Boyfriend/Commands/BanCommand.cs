using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class BanCommand : Command {
    public override string[] Aliases { get; } = { "ban", "бан" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        var toBan = cmd.GetUser(args, 0, "ToBan");
        if (toBan == null || !cmd.HasPermission(GuildPermission.BanMembers)) return;

        var memberToBan = cmd.GetMember(toBan, null);
        if (memberToBan != null && !cmd.CanInteractWith(memberToBan, "Ban")) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "BanReason");
        if (reason == null) return;

        await BanUser(cmd, toBan, duration, reason);
    }

    public static async Task BanUser(CommandProcessor cmd, SocketUser toBan, TimeSpan duration, string reason) {
        var author = cmd.Context.User;
        var guild = cmd.Context.Guild;
        await Utils.SendDirectMessage(toBan,
            string.Format(Messages.YouWereBanned, author.Mention, guild.Name, Utils.Wrap(reason)));

        var guildBanMessage = $"({author}) {reason}";
        await guild.AddBanAsync(toBan, 0, guildBanMessage);

        var feedback = string.Format(Messages.FeedbackUserBanned, toBan.Mention,
            Utils.GetHumanizedTimeOffset(duration), Utils.Wrap(reason));
        cmd.Reply(feedback, ":hammer: ");
        cmd.Audit(feedback);

        if (duration.TotalSeconds > 0) {
            var _ = async () => {
                await Task.Delay(duration);
                await UnbanCommand.UnbanUser(cmd, toBan.Id, Messages.PunishmentExpired);
            };
        }
    }
}