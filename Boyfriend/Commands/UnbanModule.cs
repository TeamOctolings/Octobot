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
    [RequireBotPermission(GuildPermission.BanMembers)]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public Task Run(string user, [Remainder] string reason) {
        var toBan = Utils.ParseUser(user).Result;
        UnbanUser(Context.Guild, Context.User, toBan, reason);
        return Task.CompletedTask;
    }

    public static async void UnbanUser(IGuild guild, IUser author, IUser toUnban, string reason) {
        var authorMention = author.Mention;
        var notification = $"{authorMention} возвращает из бана {toUnban.Mention} за {Utils.WrapInline(reason)}";
        await guild.RemoveBanAsync(toUnban);
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
    }
}