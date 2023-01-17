using Boyfriend.Data;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class BanCommand : ICommand {
    public string[] Aliases { get; } = { "ban", "бан" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        var toBan = cmd.GetUser(args, cleanArgs, 0);
        if (toBan is null || !cmd.HasPermission(GuildPermission.BanMembers)) return;

        var memberToBan = cmd.GetMember(toBan.Item1);
        if (memberToBan is not null && !cmd.CanInteractWith(memberToBan, "Ban")) return;

        var duration = CommandProcessor.GetTimeSpan(args, 1);
        var reason = cmd.GetRemaining(args, duration.TotalSeconds < 1 ? 1 : 2, "BanReason");
        if (reason is not null) await BanUserAsync(cmd, toBan, duration, reason);
    }

    private static async Task BanUserAsync(CommandProcessor cmd, Tuple<ulong, SocketUser?> toBan, TimeSpan duration,
        string reason) {
        var author = cmd.Context.User;
        var guild = cmd.Context.Guild;
        if (toBan.Item2 is not null)
            await Utils.SendDirectMessage(toBan.Item2,
                string.Format(Messages.YouWereBanned, author.Mention, guild.Name, Utils.Wrap(reason)));

        var guildBanMessage = $"({author}) {reason}";
        await guild.AddBanAsync(toBan.Item1, 0, guildBanMessage);

        var memberData = GuildData.Get(guild).MemberData[toBan.Item1];
        memberData.BannedUntil
            = duration.TotalSeconds < 1 ? DateTimeOffset.MaxValue : DateTimeOffset.Now.Add(duration);
        memberData.Roles.Clear();

        cmd.ConfigWriteScheduled = true;

        var feedback = string.Format(Messages.FeedbackUserBanned, $"<@{toBan.Item1.ToString()}>",
            Utils.GetHumanizedTimeOffset(duration), Utils.Wrap(reason));
        cmd.Reply(feedback, ReplyEmojis.Banned);
        cmd.Audit(feedback);
    }
}
