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
            var adminLogChannel = guild.GetTextChannel(config.AdminLogChannel.GetValueOrDefault(0));
            var admin = adminLogChannel == null ? Messages.ChannelNotSpecified : adminLogChannel.Mention;
            var botLogChannel = guild.GetTextChannel(config.BotLogChannel.GetValueOrDefault(0));
            var bot = botLogChannel == null ? Messages.ChannelNotSpecified : botLogChannel.Mention;
            var muteRole = guild.GetRole(config.MuteRole.GetValueOrDefault(0));
            var mute = muteRole == null ? Messages.RoleNotSpecified : muteRole.Mention;
            var defaultRole = guild.GetRole(config.DefaultRole.GetValueOrDefault(0));
            var defaultr = defaultRole == null ? Messages.RoleNotSpecified : defaultRole.Mention;
            var toSend = string.Format(Messages.CurrentSettings, nl) +
                         string.Format(Messages.CurrentSettingsLang, config.Lang, nl) +
                         string.Format(Messages.CurrentSettingsPrefix, config.Prefix, nl) +
                         string.Format(Messages.CurrentSettingsRemoveRoles, YesOrNo(
                             config.RemoveRolesOnMute.GetValueOrDefault(false)), nl) +
                         string.Format(Messages.CurrentSettingsUseSystemChannel, YesOrNo(
                                 config.UseSystemChannel.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsSendWelcomeMessages, YesOrNo(
                                 config.SendWelcomeMessages.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsReceiveStartupMessages, YesOrNo(
                             config.ReceiveStartupMessages.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsWelcomeMessage, config.WelcomeMessage, nl) +
                         string.Format(Messages.CurrentSettingsDefaultRole, defaultr, nl) +
                         string.Format(Messages.CurrentSettingsMuteRole, mute, nl) +
                         string.Format(Messages.CurrentSettingsAdminLogChannel, admin, nl) +
                         string.Format(Messages.CurrentSettingsBotLogChannel, bot);
            await Utils.SilentSendAsync(context.Channel as ITextChannel ?? throw new ApplicationException(), toSend);
            return;
        }

        var setting = args[0].ToLower();
        var value = "";
        var shouldDefault = false;
        if (args.Length >= 2)
            value = args[1].ToLower();
        else
            shouldDefault = true;

        var boolValue = ParseBool(value);
        var channel = await Utils.ParseChannelNullable(value) as IGuildChannel;
        var role = Utils.ParseRoleNullable(guild, value);

        switch (setting) {
            case "reset":
                Boyfriend.ResetGuildConfig(guild);
                break;
            case "lang" when value is not ("ru" or "en"):
                throw new ApplicationException(Messages.LanguageNotSupported);
            case "lang":
                config.Lang = shouldDefault ? "ru" : value;
                Messages.Culture = new CultureInfo(shouldDefault ? "ru" : value);
                break;
            case "prefix":
                config.Prefix = shouldDefault ? "!" : value;
                break;
            case "removerolesonmute":
                config.RemoveRolesOnMute = !shouldDefault && GetBoolValue(boolValue);
                break;
            case "usesystemchannel":
                config.UseSystemChannel = shouldDefault || GetBoolValue(boolValue);
                break;
            case "sendwelcomemessages":
                config.SendWelcomeMessages = shouldDefault || GetBoolValue(boolValue);
                break;
            case "receivestartupmessages":
                config.ReceiveStartupMessages = shouldDefault || GetBoolValue(boolValue);
                break;
            case "welcomemessage":
                config.WelcomeMessage = shouldDefault ? Messages.DefaultWelcomeMessage : value;
                break;
            case "defaultrole":
                config.DefaultRole = shouldDefault ? 0 : GetRoleId(role);
                break;
            case "muterole":
                config.MuteRole = shouldDefault ? 0 : GetRoleId(role);
                break;
            case "adminlogchannel":
                config.AdminLogChannel = shouldDefault ? 0 : GetChannelId(channel);
                break;
            case "botlogchannel":
                config.BotLogChannel = shouldDefault ? 0 : GetChannelId(channel);
                break;
            default:
                await context.Channel.SendMessageAsync(Messages.SettingDoesntExist);
                return;
        }

        await config.Save();

        await context.Channel.SendMessageAsync(Messages.SettingsUpdated);
    }

    private static bool? ParseBool(string toParse) {
        try {
            return bool.Parse(toParse.ToLower());
        } catch (FormatException) {
            return null;
        }
    }

    private static bool GetBoolValue(bool? from) {
        return from ?? throw new ApplicationException(Messages.InvalidBoolean);
    }

    private static ulong GetRoleId(IRole? role) {
        return (role ?? throw new ApplicationException(Messages.InvalidRoleSpecified)).Id;
    }

    private static ulong GetChannelId(IGuildChannel? channel) {
        return (channel ?? throw new ApplicationException(Messages.InvalidChannelSpecified)).Id;
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
