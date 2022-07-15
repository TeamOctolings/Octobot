using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class ClearCommand : Command {
    public override string[] Aliases { get; } = { "clear", "purge", "очистить", "стереть" };
    public override int ArgsLengthRequired => 1;

    public override async Task Run(SocketCommandContext context, string[] args) {
        var user = (SocketGuildUser)context.User;

        if (context.Channel is not SocketTextChannel channel) throw new Exception();

        var permissionCheckResponse = CommandHandler.HasPermission(ref user, GuildPermission.ManageMessages);
        if (permissionCheckResponse is not "") {
            Error(permissionCheckResponse, true);
            return;
        }

        if (!int.TryParse(args[0], out var toDelete)) {
            Error(Messages.ClearInvalidAmountSpecified, false);
            return;
        }

        switch (toDelete) {
            case < 1:
                Error(Messages.ClearNegativeAmount, false);
                break;
            case > 200:
                Error(Messages.ClearAmountTooLarge, false);
                break;
            default:
                var messages = await channel.GetMessagesAsync(toDelete + 1).FlattenAsync();

                await channel.DeleteMessagesAsync(messages, Utils.GetRequestOptions(user.ToString()!));

                await Utils.SendFeedback(
                    string.Format(Messages.FeedbackMessagesCleared, (toDelete + 1).ToString(), channel.Mention),
                    context.Guild.Id, user.Mention);

                break;
        }
    }
}