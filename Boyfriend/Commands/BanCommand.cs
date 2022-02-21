using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class BanCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var reason = Utils.JoinString(args, 1);

        TimeSpan duration;
        try {
            duration = Utils.GetTimeSpan(args[1]);
            reason = Utils.JoinString(args, 2);
        } catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            await Warn(context.Channel as ITextChannel, Messages.DurationParseFailed);
            duration = TimeSpan.FromMilliseconds(-1);
        }

        await BanUser(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id),
            await Utils.ParseUser(args[0]), duration, reason);
    }

    public static async Task BanUser(IGuild guild, ITextChannel? channel, IGuildUser author, IUser toBan,
        TimeSpan duration, string reason) {
        var authorMention = author.Mention;
        var guildBanMessage = $"({Utils.GetNameAndDiscrim(author)}) {reason}";
        var memberToBan = await guild.GetUserAsync(toBan.Id);
        var expiresIn = duration.TotalSeconds > 0 ? string.Format(Messages.PunishmentExpiresIn, Environment.NewLine,
            DateTimeOffset.Now.ToUnixTimeSeconds() + duration.TotalSeconds) : "";
        var notification = string.Format(Messages.UserBanned, authorMention, toBan.Mention, Utils.WrapInline(reason),
            expiresIn);

        await CommandHandler.CheckPermissions(author, GuildPermission.BanMembers);
        if (memberToBan != null)
            await CommandHandler.CheckInteractions(author, memberToBan);

        await Utils.SendDirectMessage(toBan,
            string.Format(Messages.YouWereBanned, author.Mention, guild.Name, Utils.WrapInline(reason)));

        await guild.AddBanAsync(toBan, 0, guildBanMessage);

        await Utils.SilentSendAsync(channel,
            string.Format(Messages.BanResponse, toBan.Mention, Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);

        if (duration.TotalSeconds > 0) {
            await Task.Run(async () => {
                await Task.Delay(duration);
                try {
                    await UnbanCommand.UnbanUser(guild, null, await guild.GetCurrentUserAsync(), toBan,
                        Messages.PunishmentExpired);
                } catch (ApplicationException) {}
            });
        }
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