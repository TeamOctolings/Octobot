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
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public Task Run(string user, [Remainder] string reason) {
        var toUnmute = Utils.ParseMember(Context.Guild, user).Result;
        UnmuteMember(Context.Guild, Context.User, toUnmute, reason);
        return Task.CompletedTask;
    }

    public static async void UnmuteMember(IGuild guild, IUser author, IGuildUser toUnmute, string reason) {
        var authorMention = author.Mention;
        var notification = $"{authorMention} возвращает из мута {toUnmute.Mention} за {Utils.WrapInline(reason)}";
        await toUnmute.RemoveRoleAsync(Utils.GetMuteRole(guild));
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
    }
}