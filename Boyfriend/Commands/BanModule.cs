using Discord;
using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class BanModule : ModuleBase<SocketCommandContext> {

    [Command("ban")]
    [Summary("Банит пользователя")]
    [Alias("бан")]
    public async Task Run(IUser toBan, TimeSpan duration, [Remainder]string reason)
        => await BanUser(Context.Guild, Context.User, toBan, duration, reason);

    public async void BanUser(IGuild guild, IUser author, IUser toBan, TimeSpan duration, string reason = "") {
        var authorMention = author.Mention;
        await toBan.SendMessageAsync("Тебя забанил " + authorMention + " за " + reason);
        await guild.AddBanAsync(toBan, 0, reason);
        await guild.GetSystemChannelAsync().Result.SendMessageAsync(authorMention + " банит " + toBan.Mention + " за "
                                                                    + reason);
        var banTimer = new System.Timers.Timer(duration.Milliseconds);
        banTimer.Elapsed += UnbanModule.UnbanUser(guild, author, toBan, "Время наказания истекло").;
        banTimer.Start();
    }
}