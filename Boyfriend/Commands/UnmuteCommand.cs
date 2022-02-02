using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class UnmuteCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        await UnmuteMember(context.Guild, context.Channel as ITextChannel, context.Guild.GetUser(context.User.Id),
            await Utils.ParseMember(context.Guild, args[0]), Utils.JoinString(args, 1));
    }

    public static async Task UnmuteMember(IGuild guild, ITextChannel? channel, IGuildUser author, IGuildUser toUnmute,
        string reason) {
        await CommandHandler.CheckPermissions(author, GuildPermission.ModerateMembers, GuildPermission.ManageRoles);
        await CommandHandler.CheckInteractions(author, toUnmute);
        var authorMention = author.Mention;
        var config = Boyfriend.GetGuildConfig(guild);
        var notification = string.Format(Messages.MemberUnmuted, authorMention, toUnmute.Mention,
            Utils.WrapInline(reason));
        var requestOptions = Utils.GetRequestOptions($"({Utils.GetNameAndDiscrim(author)}) {reason}");
        var role = Utils.GetMuteRole(guild);

        if (role != null) {
            if (toUnmute.RoleIds.All(x => x != role.Id)) {
                var rolesRemoved = config.RolesRemovedOnMute;

                await toUnmute.AddRolesAsync(rolesRemoved![toUnmute.Id]);
                rolesRemoved.Remove(toUnmute.Id);
                await config.Save();
                throw new ApplicationException(Messages.RolesReturned);
            }

            if (toUnmute.RoleIds.All(x => x != role.Id))
                throw new ApplicationException(Messages.MemberNotMuted);

            await toUnmute.RemoveRoleAsync(role, requestOptions);
            if (config.RolesRemovedOnMute!.ContainsKey(toUnmute.Id)) {
                await toUnmute.AddRolesAsync(config.RolesRemovedOnMute[toUnmute.Id]);
                config.RolesRemovedOnMute.Remove(toUnmute.Id);
                await config.Save();
            }
        } else {
            if (toUnmute.TimedOutUntil == null || toUnmute.TimedOutUntil.Value.ToUnixTimeMilliseconds()
                < DateTimeOffset.Now.ToUnixTimeMilliseconds())
                throw new ApplicationException(Messages.MemberNotMuted);

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
