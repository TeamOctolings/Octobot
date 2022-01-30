using System.Text.RegularExpressions;
using Boyfriend.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend;

public static class CommandHandler {
    public static readonly Command[] Commands = {
        new BanCommand(), new ClearCommand(), new HelpCommand(),
        new KickCommand(), new MuteCommand(), new PingCommand(),
        new SettingsCommand(), new UnbanCommand(), new UnmuteCommand()
    };

    public static async Task HandleCommand(SocketUserMessage message) {
        var context = new SocketCommandContext(Boyfriend.Client, message);

        foreach (var command in Commands) {
            var regex = new Regex(Regex.Escape(Boyfriend.GetGuildConfig(context.Guild).Prefix!));
            if (!command.GetAliases().Contains(regex.Replace(message.Content, "", 1).Split()[0])) continue;

            var args = message.Content.Split().Skip(1).ToArray();
            try {
                if (command.GetArgumentsAmountRequired() > args.Length)
                    throw new ApplicationException(string.Format(Messages.NotEnoughArguments,
                        command.GetArgumentsAmountRequired(), args.Length));
                await command.Run(context, args);
            }
            catch (Exception e) {
                var signature = e switch {
                    ApplicationException => ":x:",
                    UnauthorizedAccessException => ":no_entry_sign:",
                    _ => ":stop_sign:"
                };
                await context.Channel.SendMessageAsync($"{signature} `{e.Message}`");
                if (e.StackTrace != null && e is not ApplicationException or UnauthorizedAccessException)
                    await context.Channel.SendMessageAsync(Utils.Wrap(e.StackTrace));
                throw;
            }

            break;
        }
    }

    public static async Task CheckPermissions(IGuildUser user, GuildPermission toCheck,
        GuildPermission forBot = GuildPermission.StartEmbeddedActivities) {
        if (forBot == GuildPermission.StartEmbeddedActivities) forBot = toCheck;
        if (!(await user.Guild.GetCurrentUserAsync()).GuildPermissions.Has(forBot))
            throw new UnauthorizedAccessException(Messages.CommandNoPermissionBot);
        if (!user.GuildPermissions.Has(toCheck))
            throw new UnauthorizedAccessException(Messages.CommandNoPermissionUser);
    }

    public static async Task CheckInteractions(IGuildUser actor, IGuildUser target) {
        if (actor.Guild != target.Guild)
            throw new UnauthorizedAccessException(Messages.InteractionsDifferentGuilds);
        var me = await target.Guild.GetCurrentUserAsync();
        if (actor.Id == actor.Guild.OwnerId) return;
        if (target.Id == target.Guild.OwnerId)
            throw new UnauthorizedAccessException(Messages.InteractionsOwner);
        if (actor == target)
            throw new UnauthorizedAccessException(Messages.InteractionsYourself);
        if (target == me)
            throw new UnauthorizedAccessException(Messages.InteractionsMe);
        if (me.Hierarchy <= target.Hierarchy)
            throw new UnauthorizedAccessException(Messages.InteractionsFailedBot);
        if (actor.Hierarchy <= target.Hierarchy)
            throw new UnauthorizedAccessException(Messages.InteractionsFailedUser);
    }
}
