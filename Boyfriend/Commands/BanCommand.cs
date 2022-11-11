using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class BanCommand : ICommand {
    public string[] Aliases { get; } = { "ban", "бан" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var toBan = cmd.GetUser(args, cleanArgs, 0, "ToBan");
        if (toBan is null || !cmd.HasPermission(GuildPermission.BanMembers)) return;

        var memberToBan = cmd.GetMember(toBan, null);
        if (memberToBan is not null && !cmd.CanInteractWith(memberToBan, "Ban")) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "BanReason");
        if (reason is not null) await BanUserAsync(cmd, toBan, duration, reason);
    }

    private static async Task BanUserAsync(CommandProcessor cmd, SocketUser toBan, TimeSpan duration, string reason) {
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

        if (duration.TotalSeconds > 0)
            await Task.FromResult(Utils.DelayedUnbanAsync(cmd, toBan.Id, Messages.PunishmentExpired, duration));
    }
}
