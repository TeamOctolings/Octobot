using Discord;
using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class UnbanModule : ModuleBase<SocketCommandContext> {

    [Command("unban")]
    [Summary("Возвращает пользователя из бана")]
    [Alias("разбан")]
    public async Task Run(IUser toBan, TimeSpan duration, [Remainder]string reason)
        => await UnbanUser(Context.Guild, Context.User, toBan, reason);

    public async Task UnbanUser(IGuild guild, IUser author, IUser toBan, string reason = "") {
        var authorMention = author.Mention;
        await toBan.SendMessageAsync("Тебя разбанил " + authorMention + " за " + reason);
        await guild.RemoveBanAsync(toBan);
        await guild.GetSystemChannelAsync().Result.SendMessageAsync(authorMention + " возвращает из бана "
            + toBan.Mention + " за " + reason);
    }
}