using System.Text;
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

    private static readonly Dictionary<string, Regex> RegexCache = new();
    private static readonly Regex MentionRegex = new(Regex.Escape("<@855023234407333888>"));

    public static readonly StringBuilder StackedReplyMessage = new();
    public static readonly StringBuilder StackedPublicFeedback = new();
    public static readonly StringBuilder StackedPrivateFeedback = new();

#pragma warning disable CA2211
    public static bool ConfigWriteScheduled = false; // HOW IT CAN BE PRIVATE????
#pragma warning restore CA2211

    public static async Task HandleCommand(SocketUserMessage message) {
        StackedReplyMessage.Clear();
        StackedPrivateFeedback.Clear();
        StackedPublicFeedback.Clear();
        var context = new SocketCommandContext(Boyfriend.Client, message);
        var guild = context.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);

        Regex regex;
        if (RegexCache.ContainsKey(config["Prefix"])) { regex = RegexCache[config["Prefix"]]; } else {
            regex = new Regex(Regex.Escape(config["Prefix"]));
            RegexCache.Add(config["Prefix"], regex);
        }

        var list = message.Content.Split("\n");
        var currentLine = 0;
        foreach (var line in list) {
            currentLine++;
            foreach (var command in Commands) {
                var lineNoMention = MentionRegex.Replace(line, "", 1);
                if (!command.Aliases.Contains(regex.Replace(lineNoMention, "", 1).Trim().ToLower().Split()[0]))
                    continue;

                await context.Channel.TriggerTypingAsync();

                var args = line.Split().Skip(1).ToArray();

                if (command.ArgsLengthRequired <= args.Length)
                    await command.Run(context, args);
                else
                    StackedReplyMessage.AppendFormat(Messages.NotEnoughArguments, command.ArgsLengthRequired.ToString(),
                        args.Length.ToString());

                if (currentLine != list.Length) continue;
                if (ConfigWriteScheduled) await Boyfriend.WriteGuildConfig(guild.Id);
                if (StackedReplyMessage.Length > 0)
                    await message.ReplyAsync(StackedReplyMessage.ToString(), false, null, AllowedMentions.None);

                var adminChannel = Utils.GetAdminLogChannel(guild.Id);
                var systemChannel = guild.SystemChannel;
                if (StackedPrivateFeedback.Length > 0 && adminChannel != null && adminChannel.Id != message.Channel.Id)
                    await Utils.SilentSendAsync(adminChannel, StackedPrivateFeedback.ToString());
                if (StackedPublicFeedback.Length > 0 && systemChannel != null && systemChannel.Id != adminChannel?.Id
                    && systemChannel.Id != message.Channel.Id)
                    await Utils.SilentSendAsync(systemChannel, StackedPublicFeedback.ToString());
            }
        }
    }

    public static string HasPermission(ref SocketGuildUser user, GuildPermission toCheck,
        GuildPermission forBot = GuildPermission.StartEmbeddedActivities) {
        var me = user.Guild.CurrentUser;

        if (user.Id == user.Guild.OwnerId || (me.GuildPermissions.Has(GuildPermission.Administrator) &&
                                              user.GuildPermissions.Has(GuildPermission.Administrator))) return "";

        if (forBot == GuildPermission.StartEmbeddedActivities) forBot = toCheck;

        if (!me.GuildPermissions.Has(forBot))
            return Messages.CommandNoPermissionBot;

        return !user.GuildPermissions.Has(toCheck) ? Messages.CommandNoPermissionUser : "";
    }

    public static string CanInteract(ref SocketGuildUser actor, ref SocketGuildUser target) {
        if (actor.Guild != target.Guild)
            return Messages.InteractionsDifferentGuilds;
        if (actor.Id == actor.Guild.OwnerId)
            return "";

        if (target.Id == target.Guild.OwnerId)
            return Messages.InteractionsOwner;
        if (actor == target)
            return Messages.InteractionsYourself;

        var me = target.Guild.CurrentUser;

        if (target == me)
            return Messages.InteractionsMe;
        if (me.Hierarchy <= target.Hierarchy)
            return Messages.InteractionsFailedBot;

        return actor.Hierarchy <= target.Hierarchy ? Messages.InteractionsFailedUser : "";
    }
}
