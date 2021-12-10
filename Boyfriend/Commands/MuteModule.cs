using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class MuteModule : ModuleBase<SocketCommandContext> {

    [Command("mute")]
    [Summary("Глушит пользователя")]
    [Alias("мут")]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public Task Run(string user, TimeSpan duration, [Remainder]string reason) {
        var toMute = Utils.ParseMember(Context.Guild, user).Result;
        MuteMember(Context.Guild, Context.User, toMute, duration, reason);
        return Task.CompletedTask;
    }

    private static async void MuteMember(IGuild guild, IMentionable author, IGuildUser toMute, TimeSpan duration,
        string reason) {
        var authorMention = author.Mention;
        var role = Utils.GetMuteRole(guild);
        await toMute.AddRoleAsync(role);
        var notification = $"{authorMention} глушит {toMute.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
        var task = new Task(() => UnmuteModule.UnmuteMember(guild, guild.GetCurrentUserAsync().Result, toMute,
            "Время наказания истекло"));
        await Utils.StartDelayed(task, duration, () => toMute.RoleIds.Any(x => x == role.Id));
    }
}