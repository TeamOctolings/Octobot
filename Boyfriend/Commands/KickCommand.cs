using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class KickCommand : Command {
    public override string[] Aliases { get; } = { "kick", "кик", "выгнать" };
    public override int ArgsLengthRequired => 2;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var author = (SocketGuildUser)context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.KickMembers);
        if (permissionCheckResponse is not "") {
            Error(permissionCheckResponse, true);
            return;
        }

        var toKick = Utils.ParseMember(context.Guild, args[0]);

        if (toKick == null) {
            Error(Messages.UserNotInGuild, false);
            return;
        }

        var interactionCheckResponse = CommandHandler.CanInteract(ref author, ref toKick);
        if (interactionCheckResponse is not "") {
            Error(interactionCheckResponse, true);
            return;
        }

        await KickMember(context.Guild, author, toKick, Utils.JoinString(ref args, 1));

        Success(
            string.Format(Messages.FeedbackMemberKicked, toKick.Mention,
                Utils.Wrap(Utils.JoinString(ref args, 1))), author.Mention);
    }

    private static async Task KickMember(IGuild guild, SocketUser author, SocketGuildUser toKick, string reason) {
        var authorMention = author.Mention;
        var guildKickMessage = $"({author}) {reason}";

        await Utils.SendDirectMessage(toKick,
            string.Format(Messages.YouWereKicked, authorMention, guild.Name, Utils.Wrap(reason)));

        await toKick.KickAsync(guildKickMessage);
    }
}