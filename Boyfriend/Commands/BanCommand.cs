using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class BanCommand : Command {
    public override string[] Aliases { get; } = { "ban", "бан" };
    public override int ArgsLengthRequired => 2;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var toBan = Utils.ParseUser(args[0]);

        if (toBan == null) {
            Error(Messages.UserDoesntExist, false);
            return;
        }

        var guild = context.Guild;
        var author = (SocketGuildUser)context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.BanMembers);
        if (permissionCheckResponse is not "") {
            Error(permissionCheckResponse, true);
            return;
        }

        var reason = Utils.JoinString(ref args, 2);
        var memberToBan = Utils.ParseMember(guild, args[0]);

        if (memberToBan != null) {
            var interactionCheckResponse = CommandHandler.CanInteract(ref author, ref memberToBan);
            if (interactionCheckResponse is not "") {
                Error(interactionCheckResponse, true);
                return;
            }
        }

        var duration = Utils.GetTimeSpan(ref args[1]) ?? TimeSpan.FromMilliseconds(-1);
        if (duration.TotalSeconds < 0) {
            Warn(Messages.DurationParseFailed);
            reason = Utils.JoinString(ref args, 1);

            if (reason is "") {
                Error(Messages.ReasonRequired, false);
                return;
            }
        }

        await BanUser(guild, author, toBan, duration, reason);
    }

    public static async Task BanUser(SocketGuild guild, SocketGuildUser author, SocketUser toBan, TimeSpan duration,
        string reason) {
        var guildBanMessage = $"({author}) {reason}";

        await Utils.SendDirectMessage(toBan,
            string.Format(Messages.YouWereBanned, author.Mention, guild.Name, Utils.Wrap(reason)));

        await guild.AddBanAsync(toBan, 0, guildBanMessage);

        var feedback = string.Format(Messages.FeedbackUserBanned, toBan.Mention,
            Utils.GetHumanizedTimeOffset(ref duration), Utils.Wrap(reason));
        Success(feedback, author.Mention, false, false);
        await Utils.SendFeedback(feedback, guild.Id, author.Mention, true);

        if (duration.TotalSeconds > 0) {
            var _ = async () => {
                await Task.Delay(duration);
                await UnbanCommand.UnbanUser(guild, guild.CurrentUser, toBan, Messages.PunishmentExpired);
            };
        }
    }
}
