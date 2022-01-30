using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class MuteCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        TimeSpan duration;
        var reason = Utils.JoinString(args, 1);
        try {
            duration = Utils.GetTimeSpan(args[1]);
            reason = Utils.JoinString(args, 2);
        }
        catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            duration = TimeSpan.FromMilliseconds(-1);
        }

        var author = context.Guild.GetUser(context.User.Id);
        var toMute = await Utils.ParseMember(context.Guild, args[0]);
        if (toMute == null)
            throw new ApplicationException(Messages.UserNotInGuild);
        var role = Utils.GetMuteRole(context.Guild);
        if (role != null && toMute.RoleIds.Any(x => x == role.Id) ||
            toMute.TimedOutUntil != null && toMute.TimedOutUntil.Value.ToUnixTimeMilliseconds()
            > DateTimeOffset.Now.ToUnixTimeMilliseconds())
            throw new ApplicationException(Messages.MemberAlreadyMuted);
        var config = Boyfriend.GetGuildConfig(context.Guild);
        var rolesRemoved = config.RolesRemovedOnMute!;
        if (rolesRemoved.ContainsKey(toMute.Id)) {
            foreach (var roleId in rolesRemoved[toMute.Id]) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            await config.Save();
            await Warn(context.Channel, Messages.RolesReturned);
            return;
        }

        await CommandHandler.CheckPermissions(author, GuildPermission.ModerateMembers, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toMute);
        MuteMember(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id), toMute,
            duration, reason);
    }

    private static async void MuteMember(IGuild guild, ITextChannel? channel, IGuildUser author, IGuildUser toMute,
        TimeSpan duration, string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var role = Utils.GetMuteRole(guild);
        var config = Boyfriend.GetGuildConfig(guild);
        if (config.RemoveRolesOnMute.GetValueOrDefault(false) && role != null) {
            var rolesRemoved = new List<ulong>();
            try {
                foreach (var roleId in toMute.RoleIds) {
                    if (roleId == guild.Id) continue;
                    await toMute.RemoveRoleAsync(roleId);
                    rolesRemoved.Add(roleId);
                }
            }
            catch (NullReferenceException) { }

            config.RolesRemovedOnMute!.Add(toMute.Id, rolesRemoved);
            await config.Save();
        }

        if (role != null)
            await toMute.AddRoleAsync(role);
        else
            await toMute.SetTimeOutAsync(duration);
        var notification = string.Format(Messages.MemberMuted, authorMention, toMute.Mention, Utils.WrapInline(reason));
        await Utils.SilentSendAsync(channel, string.Format(Messages.MuteResponse, toMute.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
        var task = new Task(() => UnmuteCommand.UnmuteMember(guild, null, guild.GetCurrentUserAsync().Result, toMute,
            Messages.PunishmentExpired));
        if (role != null)
            await Utils.StartDelayed(task, duration, () => toMute.RoleIds.Any(x => x == role.Id));
    }

    public override List<string> GetAliases() {
        return new List<string> {"mute", "мут", "мьют"};
    }

    public override int GetArgumentsAmountRequired() {
        return 2;
    }

    public override string GetSummary() {
        return "Глушит участника";
    }
}
