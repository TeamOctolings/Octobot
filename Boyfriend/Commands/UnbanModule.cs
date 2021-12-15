using Discord;
using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class UnbanModule : ModuleBase<SocketCommandContext> {

    [Command("unban")]
    [Summary("Возвращает пользователя из бана")]
    [Alias("разбан")]
    public async Task Run(string user, [Remainder] string reason) {
        var toUnban = await Utils.ParseUser(user);
        if (Context.Guild.GetBanAsync(toUnban.Id) == null)
            throw new Exception("Пользователь не забанен!");
        UnbanUser(Context.Guild, Context.Guild.GetUser(Context.User.Id), toUnban, reason);
    }

    public static async void UnbanUser(IGuild guild, IGuildUser author, IUser toUnban, string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.BanMembers);
        var authorMention = author.Mention;
        var notification = $"{authorMention} возвращает из бана {toUnban.Mention} за {Utils.WrapInline(reason)}";
        await guild.RemoveBanAsync(toUnban);
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
    }
}