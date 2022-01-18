using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class BanCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var toBan = await Utils.ParseUser(args[0]);
        var reason = Utils.JoinString(args, 1);
        TimeSpan duration;
        try {
            duration = Utils.GetTimeSpan(args[1]);
            reason = Utils.JoinString(args, 2);
        }
        catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            duration = TimeSpan.FromMilliseconds(-1);
        }

        var author = context.Guild.GetUser(context.User.Id);

        await CommandHandler.CheckPermissions(author, GuildPermission.BanMembers);
        var memberToBan = context.Guild.GetUser(toBan.Id);
        if (memberToBan != null)
            await CommandHandler.CheckInteractions(author, memberToBan);
        await BanUser(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id),
            toBan, duration, reason);
    }

    public static async Task BanUser(IGuild guild, ITextChannel? channel, IGuildUser author, IUser toBan,
        TimeSpan duration, string reason) {
        var authorMention = author.Mention;
        await Utils.SendDirectMessage(toBan, string.Format(Messages.YouWereBanned, author.Mention, guild.Name,
            Utils.WrapInline(reason)));
        var guildBanMessage = $"({author.Username}#{author.Discriminator}) {reason}";
        await guild.AddBanAsync(toBan, 0, guildBanMessage);
        var notification = string.Format(Messages.UserBanned, authorMention, toBan.Mention, Utils.WrapInline(reason));
        await Utils.SilentSendAsync(channel, string.Format(Messages.BanResponse, toBan.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
        var task = new Task(() => UnbanCommand.UnbanUser(guild, null, guild.GetCurrentUserAsync().Result, toBan,
            Messages.PunishmentExpired));
        await Utils.StartDelayed(task, duration, () => guild.GetBanAsync(toBan).Result != null);
    }

    public override List<string> GetAliases() {
        return new List<string> {"ban", "бан"};
    }

    public override int GetArgumentsAmountRequired() {
        return 2;
    }

    public override string GetSummary() {
        return "Банит пользователя";
    }
}