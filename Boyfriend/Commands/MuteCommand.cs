using Discord;
using Discord.Commands;
using Discord.Net;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class MuteCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var author = context.Guild.GetUser(context.User.Id);
        var config = Boyfriend.GetGuildConfig(context.Guild);
        var reason = Utils.JoinString(args, 1);
        var role = Utils.GetMuteRole(context.Guild);
        var rolesRemoved = config.RolesRemovedOnMute!;
        var toMute = await Utils.ParseMember(context.Guild, args[0]);

        TimeSpan duration;
        try {
            duration = Utils.GetTimeSpan(args[1]);
            reason = Utils.JoinString(args, 2);
        } catch (Exception e) when (e is ArgumentNullException or FormatException or OverflowException) {
            duration = TimeSpan.FromMilliseconds(-1);
        }

        if (toMute == null)
            throw new ApplicationException(Messages.UserNotInGuild);

        if (role != null && toMute.RoleIds.Any(x => x == role.Id) ||
            toMute.TimedOutUntil != null && toMute.TimedOutUntil.Value.ToUnixTimeMilliseconds()
            > DateTimeOffset.Now.ToUnixTimeMilliseconds())
            throw new ApplicationException(Messages.MemberAlreadyMuted);

        if (rolesRemoved.ContainsKey(toMute.Id)) {
            foreach (var roleId in rolesRemoved[toMute.Id]) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            await config.Save();
            await Warn(context.Channel, Messages.RolesReturned);
            return;
        }

        await CommandHandler.CheckPermissions(author, GuildPermission.ModerateMembers, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toMute);

        await MuteMember(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id), toMute,
            duration, reason);
    }

    private static async Task MuteMember(IGuild guild, ITextChannel? channel, IGuildUser author, IGuildUser toMute,
        TimeSpan duration, string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var config = Boyfriend.GetGuildConfig(guild);
        var requestOptions = Utils.GetRequestOptions($"({Utils.GetNameAndDiscrim(author)}) {reason}");
        var role = Utils.GetMuteRole(guild);

        if (role != null) {
            if (config.RemoveRolesOnMute.GetValueOrDefault(false)) {
                var rolesRemoved = new List<ulong>();
                foreach (var roleId in toMute.RoleIds) {
                    try {
                        if (roleId == guild.Id) continue;
                        if (roleId == role.Id) continue;
                        await toMute.RemoveRoleAsync(roleId);
                        rolesRemoved.Add(roleId);
                    } catch (HttpException) {}
                }

                config.RolesRemovedOnMute!.Add(toMute.Id, rolesRemoved);
                await config.Save();
            }

            await toMute.AddRoleAsync(role, requestOptions);
        } else
            await toMute.SetTimeOutAsync(duration, requestOptions);
        var notification = string.Format(Messages.MemberMuted, authorMention, toMute.Mention, Utils.WrapInline(reason));
        await Utils.SilentSendAsync(channel, string.Format(Messages.MuteResponse, toMute.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);

        async void UnmuteWhenExpires() {
            try {
                await UnmuteCommand.UnmuteMember(guild, null, await guild.GetCurrentUserAsync(), toMute,
                    Messages.PunishmentExpired);
            } catch (ApplicationException) {}
        }

        if (role != null)
            await Utils.StartDelayed(new Task(UnmuteWhenExpires), duration);
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
