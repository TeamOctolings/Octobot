using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class ClearCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var user = context.User;

        int toDelete;
        try {
            toDelete = Convert.ToInt32(args[0]);
        } catch (Exception e) when (e is FormatException or OverflowException) {
            throw new ApplicationException(Messages.ClearInvalidAmountSpecified);
        }

        if (context.Channel is not ITextChannel channel) return;

        await CommandHandler.CheckPermissions(context.Guild.GetUser(user.Id), GuildPermission.ManageMessages);

        switch (toDelete) {
            case < 1:
                throw new ApplicationException(Messages.ClearNegativeAmount);
            case > 200:
                throw new ApplicationException(Messages.ClearAmountTooLarge);
            default: {
                var messages = await channel.GetMessagesAsync(toDelete + 1).FlattenAsync();

                await channel.DeleteMessagesAsync(messages, Utils.GetRequestOptions(Utils.GetNameAndDiscrim(user)));

                await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(context.Guild),
                    string.Format(Messages.MessagesDeleted, user.Mention, toDelete + 1,
                        Utils.MentionChannel(context.Channel.Id)));
                break;
            }
        }
    }

    public override List<string> GetAliases() {
        return new List<string> {"clear", "purge", "очистить", "стереть"};
    }

    public override int GetArgumentsAmountRequired() {
        return 1;
    }

    public override string GetSummary() {
        return "Очищает сообщения";
    }
}
