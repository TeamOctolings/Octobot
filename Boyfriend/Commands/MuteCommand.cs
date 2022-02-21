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
            await Warn(context.Channel as ITextChannel, Messages.DurationParseFailed);
            duration = TimeSpan.FromMilliseconds(-1);
        }

        if (toMute == null)
            throw new ApplicationException(Messages.UserNotInGuild);

        if (role != null && toMute.RoleIds.Any(x => x == role.Id) || toMute.TimedOutUntil != null &&
            toMute.TimedOutUntil.Value.ToUnixTimeMilliseconds() > DateTimeOffset.Now.ToUnixTimeMilliseconds())
            throw new ApplicationException(Messages.MemberAlreadyMuted);

        if (rolesRemoved.ContainsKey(toMute.Id)) {
            foreach (var roleId in rolesRemoved[toMute.Id]) await toMute.AddRoleAsync(roleId);
            rolesRemoved.Remove(toMute.Id);
            await config.Save();
            throw new ApplicationException(Messages.RolesReturned);
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
        var hasDuration = duration.TotalSeconds > 0;
        var expiresIn = hasDuration ? string.Format(Messages.PunishmentExpiresIn, Environment.NewLine,
            DateTimeOffset.Now.ToUnixTimeSeconds() + duration.TotalSeconds) : "";
        var notification = string.Format(Messages.MemberMuted, authorMention, toMute.Mention, Utils.WrapInline(reason),
            expiresIn);

        if (role != null) {
            if (config.RemoveRolesOnMute.GetValueOrDefault(false)) {
                var rolesRemoved = new List<ulong>();
                foreach (var roleId in toMute.RoleIds) {
                    try {
                        if (roleId == guild.Id) continue;
                        if (roleId == role.Id) continue;
                        await toMute.RemoveRoleAsync(roleId);
                        rolesRemoved.Add(roleId);
                    } catch (HttpException e) {
                        await Warn(channel,
                            string.Format(Messages.RoleRemovalFailed, $"<@&{roleId}>", Utils.WrapInline(e.Reason)));
                    }
                }

                config.RolesRemovedOnMute!.Add(toMute.Id, rolesRemoved);
                await config.Save();

                if (hasDuration)
                    await Task.Run(async () => {
                        await Task.Delay(duration);
                        try {
                            await UnmuteCommand.UnmuteMember(guild, null, await guild.GetCurrentUserAsync(), toMute,
                                Messages.PunishmentExpired);
                        } catch (ApplicationException) {}
                    });
            }

            await toMute.AddRoleAsync(role, requestOptions);
        } else {
            if (!hasDuration)
                throw new ApplicationException(Messages.DurationRequiredForTimeOuts);
            if (toMute.IsBot)
                throw new ApplicationException(Messages.CannotTimeOutBot);

            await toMute.SetTimeOutAsync(duration, requestOptions);
        }
        await Utils.SilentSendAsync(channel,
            string.Format(Messages.MuteResponse, toMute.Mention, Utils.WrapInline(reason)));
        await Utils.SilentSendAsync(await guild.GetSystemChannelAsync(), notification);
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(guild), notification);
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