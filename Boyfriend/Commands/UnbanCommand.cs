using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class UnbanCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        await UnbanUser(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id),
            await Utils.ParseUser(args[0]), Utils.JoinString(args, 1));
    }

    public static async Task UnbanUser(IGuild guild, ITextChannel? channel, IGuildUser author, IUser toUnban,
        string reason) {

        var authorMention = author.Mention;
        var notification = string.Format(Messages.UserUnbanned, authorMention, toUnban.Mention,
            Utils.WrapInline(reason));
        var requestOptions = Utils.GetRequestOptions($"({Utils.GetNameAndDiscrim(author)}) {reason}");

        await CommandHandler.CheckPermissions(author, GuildPermission.BanMembers);

        if (guild.GetBanAsync(toUnban.Id) == null)
            throw new ApplicationException(Messages.UserNotBanned);

        await guild.RemoveBanAsync(toUnban, requestOptions);

        await Utils.SilentSendAsync(channel, string.Format(Messages.UnbanResponse, toUnban.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
    }

    public override List<string> GetAliases() {
        return new List<string> {"unban", "разбан"};
    }

    public override int GetArgumentsAmountRequired() {
        return 2;
    }

    public override string GetSummary() {
        return "Возвращает пользователя из бана";
    }
}
