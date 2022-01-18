using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class KickCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var reason = Utils.JoinString(args, 1);
        var author = context.Guild.GetUser(context.User.Id);
        var toKick = await Utils.ParseMember(context.Guild, args[0]);
        await CommandHandler.CheckPermissions(author, GuildPermission.KickMembers);
        await CommandHandler.CheckInteractions(author, toKick);
        KickMember(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id), toKick,
            reason);
    }

    private static async void KickMember(IGuild guild, ITextChannel? channel, IUser author, IGuildUser toKick,
        string reason) {
        var authorMention = author.Mention;
        await Utils.SendDirectMessage(toKick, string.Format(Messages.YouWereKicked, authorMention, guild.Name,
            Utils.WrapInline(reason)));
        var guildKickMessage = $"({author.Username}#{author.Discriminator}) {reason}";
        await toKick.KickAsync(guildKickMessage);
        var notification = string.Format(Messages.MemberKicked, authorMention, toKick.Mention,
            Utils.WrapInline(reason));
        await Utils.SilentSendAsync(channel, string.Format(Messages.KickResponse, toKick.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
    }

    public override List<string> GetAliases() {
        return new List<string> {"kick", "кик"};
    }

    public override int GetArgumentsAmountRequired() {
        return 2;
    }

    public override string GetSummary() {
        return "Выгоняет участника";
    }
}