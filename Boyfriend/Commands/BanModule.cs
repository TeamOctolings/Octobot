using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class BanModule : ModuleBase<SocketCommandContext> {

    [Command("ban")]
    [Summary("Банит пользователя")]
    [Alias("бан")]
    public async Task Run(string user, [Remainder]string reason) {
        TimeSpan duration;
        try {
            var reasonArray = reason.Split();
            duration = Utils.GetTimeSpan(reasonArray[0]);
            reason = string.Join(" ", reasonArray.Skip(1));
        } catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            duration = TimeSpan.FromMilliseconds(-1);
        }
        var author = Context.Guild.GetUser(Context.User.Id);
        var toBan = await Utils.ParseUser(user);
        await CommandHandler.CheckPermissions(author, GuildPermission.BanMembers);
        var memberToBan = Context.Guild.GetUser(toBan.Id);
        if (memberToBan != null)
            await CommandHandler.CheckInteractions(author, memberToBan);
        BanUser(Context.Guild, Context.Guild.GetUser(Context.User.Id), toBan, duration, reason);
    }

    public static async void BanUser(IGuild guild, IGuildUser author, IUser toBan, TimeSpan duration, string reason) {
        var authorMention = author.Mention;
        await Utils.SendDirectMessage(toBan, $"Тебя забанил {author.Mention} на сервере {guild.Name} за `{reason}`");
        var guildBanMessage = $"({author.Username}#{author.Discriminator}) {reason}";
        await guild.AddBanAsync(toBan, 0, guildBanMessage);
        var notification = $"{authorMention} банит {toBan.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
        var task = new Task(() => UnbanModule.UnbanUser(guild, guild.GetCurrentUserAsync().Result, toBan,
            "Время наказания истекло"));
        await Utils.StartDelayed(task, duration, () => guild.GetBanAsync(toBan).Result != null);
    }
}