using System.Globalization;
using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class SettingsCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        await CommandHandler.CheckPermissions(context.Guild.GetUser(context.User.Id), GuildPermission.ManageGuild);
        var config = Boyfriend.GetGuildConfig(context.Guild);
        var guild = context.Guild;
        if (args.Length == 0) {
            var nl = Environment.NewLine;
            var adminLogChannel = guild.GetTextChannel(config.AdminLogChannel);
            var admin = adminLogChannel == null ? Messages.ChannelNotSpecified : adminLogChannel.Mention;
            var botLogChannel = guild.GetTextChannel(config.BotLogChannel);
            var bot = botLogChannel == null ? Messages.ChannelNotSpecified : botLogChannel.Mention;
            var muteRole = guild.GetRole(config.MuteRole);
            var mute = muteRole == null ? Messages.RoleNotSpecified : muteRole.Mention;
            var defaultRole = guild.GetRole(config.DefaultRole);
            var defaultr = muteRole == null ? Messages.RoleNotSpecified : defaultRole.Mention;
            var toSend = string.Format(Messages.CurrentSettings, nl) +
                         string.Format(Messages.CurrentSettingsLang, config.Lang, nl) +
                         string.Format(Messages.CurrentSettingsPrefix, config.Prefix, nl) +
                         string.Format(Messages.CurrentSettingsRemoveRoles, YesOrNo(config.RemoveRolesOnMute), nl) +
                         string.Format(Messages.CurrentSettingsUseSystemChannel, YesOrNo(config.UseSystemChannel), nl) +
                         string.Format(Messages.CurrentSettingsSendWelcomeMessages, YesOrNo(config.UseSystemChannel),
                             nl) +
                         string.Format(Messages.CurrentSettingsWelcomeMessage, config.WelcomeMessage, nl) +
                         string.Format(Messages.CurrentSettingsDefaultRole, defaultr, nl) +
                         string.Format(Messages.CurrentSettingsMuteRole, mute, nl) +
                         string.Format(Messages.CurrentSettingsAdminLogChannel, admin, nl) +
                         string.Format(Messages.CurrentSettingsBotLogChannel, bot);
            await Utils.SilentSendAsync(context.Channel as ITextChannel ?? throw new Exception(), toSend);
            return;
        }

        var setting = args[0].ToLower();
        var value = args[1].ToLower();

        var boolValue = ParseBool(args[1]);
        var channel = await Utils.ParseChannelNullable(value) as IGuildChannel;
        var role = Utils.ParseRoleNullable(guild, value);

        switch (setting) {
            case "lang" when value is not ("ru" or "en"):
                throw new Exception(Messages.LanguageNotSupported);
            case "lang":
                config.Lang = value;
                Messages.Culture = new CultureInfo(value);
                break;
            case "prefix":
                config.Prefix = value;
                break;
            case "removerolesonmute":
                config.RemoveRolesOnMute = GetBoolValue(boolValue);
                break;
            case "usesystemchannel":
                config.UseSystemChannel = GetBoolValue(boolValue);
                break;
            case "sendwelcomemessages":
                config.SendWelcomeMessages = GetBoolValue(boolValue);
                break;
            case "welcomemessage":
                config.WelcomeMessage = value;
                break;
            case "defaultrole":
                config.DefaultRole = GetRoleId(role);
                break;
            case "muterole":
                config.MuteRole = GetRoleId(role);
                break;
            case "adminlogchannel":
                config.AdminLogChannel = GetChannelId(channel);
                break;
            case "botlogchannel":
                config.BotLogChannel = GetChannelId(channel);
                break;
        }

        await config.Save();

        await context.Channel.SendMessageAsync(Messages.SettingsUpdated);
    }

    private static bool? ParseBool(string toParse) {
        try {
            return bool.Parse(toParse.ToLower());
        }
        catch (FormatException) {
            return null;
        }
    }

    private static bool GetBoolValue(bool? from) {
        return from ?? throw new Exception(Messages.InvalidBoolean);
    }

    private static ulong GetRoleId(IRole? role) {
        return (role ?? throw new Exception(Messages.InvalidRoleSpecified)).Id;
    }

    private static ulong GetChannelId(IGuildChannel? channel) {
        return (channel ?? throw new Exception(Messages.InvalidChannelSpecified)).Id;
    }

    private static string YesOrNo(bool isYes) {
        return isYes ? Messages.Yes : Messages.No;
    }

    public override List<string> GetAliases() {
        return new List<string> {"settings", "настройки", "config", "конфиг "};
    }

    public override int GetArgumentsAmountRequired() {
        return 0;
    }

    public override string GetSummary() {
        return "Настраивает бота отдельно для этого сервера";
    }
}