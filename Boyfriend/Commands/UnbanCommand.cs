using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class UnbanCommand : Command {
    public override string[] Aliases { get; } = { "unban", "разбан" };
    public override int ArgsLengthRequired => 2;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var author = (SocketGuildUser)context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.BanMembers);
        if (permissionCheckResponse != "") {
            Error(permissionCheckResponse, true);
            return;
        }

        var toUnban = Utils.ParseUser(args[0]);

        if (toUnban == null) {
            Error(Messages.UserDoesntExist, false);
            return;
        }

        var reason = Utils.JoinString(ref args, 1);

        await UnbanUser(context.Guild, author, toUnban, reason);
    }

    public static async Task UnbanUser(SocketGuild guild, SocketGuildUser author, SocketUser toUnban, string reason) {
        if (guild.GetBanAsync(toUnban.Id) == null) {
            Error(Messages.UserNotBanned, false);
            return;
        }

        var requestOptions = Utils.GetRequestOptions($"({author}) {reason}");
        await guild.RemoveBanAsync(toUnban, requestOptions);

        var feedback = string.Format(Messages.FeedbackUserUnbanned, toUnban.Mention, Utils.Wrap(reason));
        Success(feedback, author.Mention, false, false);
        await Utils.SendFeedback(feedback, guild.Id, author.Mention, true);
    }
}