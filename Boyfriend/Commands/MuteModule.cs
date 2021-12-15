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
    public async Task Run(string user, string durationString, [Remainder]string reason) {
        TimeSpan duration;
        try {
            duration = TimeSpan.Parse(durationString);
        } catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            duration = TimeSpan.FromMilliseconds(-1);
            reason = durationString + reason;
        }
        var author = Context.Guild.GetUser(Context.User.Id);
        var toMute = await Utils.ParseMember(Context.Guild, user);
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toMute);
        MuteMember(Context.Guild, Context.Guild.GetUser(Context.User.Id), toMute, duration, reason);
    }

    private static async void MuteMember(IGuild guild, IGuildUser author, IGuildUser toMute, TimeSpan duration,
        string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var role = Utils.GetMuteRole(guild);
        if (Boyfriend.GetGuildConfig(guild).RemoveRolesOnMute) {
            foreach (var roleId in toMute.RoleIds) {
                await toMute.RemoveRoleAsync(roleId);

            }
        }

        await toMute.AddRoleAsync(role);
        var notification = $"{authorMention} глушит {toMute.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
        var task = new Task(() => UnmuteModule.UnmuteMember(guild, guild.GetCurrentUserAsync().Result, toMute,
            "Время наказания истекло"));
        await Utils.StartDelayed(task, duration, () => toMute.RoleIds.Any(x => x == role.Id));
    }
}