using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class UnmuteCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var toUnmute = await Utils.ParseMember(context.Guild, args[0]);
        var author = context.Guild.GetUser(context.User.Id);
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toUnmute);
        var role = Utils.GetMuteRole(context.Guild);
        if (role != null) {
            if (toUnmute.RoleIds.All(x => x != role.Id)) {
                var config = Boyfriend.GetGuildConfig(context.Guild);
                var rolesRemoved = config.RolesRemovedOnMute;

                foreach (var roleId in rolesRemoved![toUnmute.Id]) await toUnmute.AddRoleAsync(roleId);
                rolesRemoved.Remove(toUnmute.Id);
                await config.Save();
                throw new ApplicationException(Messages.RolesReturned);
            }
        }
        if (role != null && toUnmute.RoleIds.All(x => x != role.Id) ||
            toUnmute.TimedOutUntil == null || toUnmute.TimedOutUntil.Value.ToUnixTimeMilliseconds()
            < DateTimeOffset.Now.ToUnixTimeMilliseconds())
            throw new ApplicationException(Messages.MemberNotMuted);

        UnmuteMember(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id),
            toUnmute, Utils.JoinString(args, 1));
    }

    public static async void UnmuteMember(IGuild guild, ITextChannel? channel, IGuildUser author, IGuildUser toUnmute,
        string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ManageMessages, GuildPermission.ManageRoles);
        var authorMention = author.Mention;
        var notification = string.Format(Messages.MemberUnmuted, authorMention, toUnmute.Mention,
            Utils.WrapInline(reason));
        var role = Utils.GetMuteRole(guild);

        if (role != null) {
            await toUnmute.RemoveRoleAsync(role);
            var config = Boyfriend.GetGuildConfig(guild);

            if (config.RolesRemovedOnMute!.ContainsKey(toUnmute.Id)) {
                foreach (var roleId in config.RolesRemovedOnMute[toUnmute.Id]) await toUnmute.AddRoleAsync(roleId);
                config.RolesRemovedOnMute.Remove(toUnmute.Id);
                await config.Save();
            }
        } else {
            await toUnmute.RemoveTimeOutAsync();
        }

        await Utils.SilentSendAsync(channel, string.Format(Messages.UnmuteResponse, toUnmute.Mention,
            Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
    }

    public override List<string> GetAliases() {
        return new List<string> {"unmute", "размут"};
    }

    public override int GetArgumentsAmountRequired() {
        return 2;
    }

    public override string GetSummary() {
        return "Снимает мут с участника";
    }
}
