using Discord;
using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class UnmuteModule : ModuleBase<SocketCommandContext> {

    [Command("unmute")]
    [Summary("Возвращает пользователя из мута")]
    [Alias("размут")]
    public async Task Run(string user, [Remainder] string reason) {
        var toUnmute = Utils.ParseMember(Context.Guild, user).Result;
        var author = Context.Guild.GetUser(Context.User.Id);
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toUnmute);
        if (toUnmute.RoleIds.All(x => x != Utils.GetMuteRole(Context.Guild).Id))
            throw new Exception("Пользователь не в муте!");
        UnmuteMember(Context.Guild, Context.Guild.GetUser(Context.User.Id), toUnmute, reason);
    }

    public static async void UnmuteMember(IGuild guild, IGuildUser author, IGuildUser toUnmute, string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var notification = $"{authorMention} возвращает из мута {toUnmute.Mention} за {Utils.WrapInline(reason)}";
        await toUnmute.RemoveRoleAsync(Utils.GetMuteRole(guild));
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
    }
}