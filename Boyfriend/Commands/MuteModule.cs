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
        var toMute = await Utils.ParseMember(Context.Guild, user);
        if (toMute.RoleIds.Any(x => x == Utils.GetMuteRole(Context.Guild).Id))
            throw new Exception("Участник уже заглушен!");
        if (Boyfriend.GetGuildConfig(Context.Guild).RolesRemovedOnMute.ContainsKey(toMute.Id))
            throw new Exception("Кто-то убрал роль мута самостоятельно!");
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toMute);
        MuteMember(Context.Guild, Context.Guild.GetUser(Context.User.Id), toMute, duration, reason);
    }

    private static async void MuteMember(IGuild guild, IGuildUser author, IGuildUser toMute, TimeSpan duration,
        string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var role = Utils.GetMuteRole(guild);
        var config = Boyfriend.GetGuildConfig(guild);

        if (config.RemoveRolesOnMute) {
            var rolesRemoved = new List<ulong>();
            try {
                foreach (var roleId in toMute.RoleIds) {
                    if (roleId == guild.Id) continue;
                    await toMute.RemoveRoleAsync(roleId);
                    rolesRemoved.Add(roleId);
                }
            } catch (NullReferenceException) {}
            config.RolesRemovedOnMute.Add(toMute.Id, rolesRemoved);
            config.Save();
        }

        await toMute.AddRoleAsync(role);
        var notification = $"{authorMention} глушит {toMute.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
        var task = new Task(() => UnmuteModule.UnmuteMember(guild, guild.GetCurrentUserAsync().Result, toMute,
            "Время наказания истекло"));
        await Utils.StartDelayed(task, duration, () => toMute.RoleIds.Any(x => x == role.Id));
    }
}