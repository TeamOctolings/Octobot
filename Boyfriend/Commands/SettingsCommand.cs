using System.Reflection;
using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class SettingsCommand : Command {
    public override async Task Run(SocketCommandContext context, string[] args) {
        var config = Boyfriend.GetGuildConfig(context.Guild);
        var guild = context.Guild;

        await CommandHandler.CheckPermissions(context.Guild.GetUser(context.User.Id), GuildPermission.ManageGuild);

        if (args.Length == 0) {
            var nl = Environment.NewLine;
            dynamic forCheck;
            var adminLogChannel = (forCheck = guild.GetTextChannel(config.AdminLogChannel.GetValueOrDefault(0))) == null
                ? Messages.ChannelNotSpecified : forCheck.Mention;
            var botLogChannel = (forCheck = guild.GetTextChannel(config.BotLogChannel.GetValueOrDefault(0))) == null
                ? Messages.ChannelNotSpecified : forCheck.Mention;
            var muteRole = (forCheck = guild.GetRole(config.MuteRole.GetValueOrDefault(0))) == null
                ? Messages.RoleNotSpecified : forCheck.Mention;
            var defaultRole = (forCheck = guild.GetRole(config.DefaultRole.GetValueOrDefault(0))) == null
                ? Messages.RoleNotSpecified : forCheck.Mention;
            var toSend = string.Format(Messages.CurrentSettings, nl) +
                         string.Format(Messages.CurrentSettingsLang, config.Lang, nl) +
                         string.Format(Messages.CurrentSettingsPrefix, config.Prefix, nl) +
                         string.Format(Messages.CurrentSettingsRemoveRoles,
                             YesOrNo(config.RemoveRolesOnMute.GetValueOrDefault(false)), nl) +
                         string.Format(Messages.CurrentSettingsUseSystemChannel,
                             YesOrNo(config.UseSystemChannel.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsSendWelcomeMessages,
                             YesOrNo(config.SendWelcomeMessages.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsReceiveStartupMessages,
                             YesOrNo(config.ReceiveStartupMessages.GetValueOrDefault(true)), nl) +
                         string.Format(Messages.CurrentSettingsWelcomeMessage, config.WelcomeMessage, nl) +
                         string.Format(Messages.CurrentSettingsDefaultRole, defaultRole, nl) +
                         string.Format(Messages.CurrentSettingsMuteRole, muteRole, nl) +
                         string.Format(Messages.CurrentSettingsAdminLogChannel, adminLogChannel, nl) +
                         string.Format(Messages.CurrentSettingsBotLogChannel, botLogChannel);
            await Utils.SilentSendAsync(context.Channel as ITextChannel ?? throw new ApplicationException(), toSend);
            return;
        }

        var setting = args[0].ToLower();
        var value = "";

        if (args.Length >= 2)
            try {
                value = args[1].ToLower();
            } catch (IndexOutOfRangeException) {
                throw new ApplicationException(Messages.InvalidSettingValue);
            }

        PropertyInfo? property = null;
        foreach (var prop in typeof(GuildConfig).GetProperties())
            if (setting == prop.Name.ToLower())
                property = prop;
        if (property == null || !property.CanWrite)
            throw new ApplicationException(Messages.SettingDoesntExist);
        var type = property.PropertyType;

        if (value is "reset" or "default") {
            property.SetValue(config, null);
        } else if (type == typeof(string)) {
            if (setting == "lang" && value is not ("ru" or "en"))
                throw new ApplicationException(Messages.LanguageNotSupported);
            property.SetValue(config, value);
        } else {
            try {
                if (type == typeof(bool?))
                    property.SetValue(config, Convert.ToBoolean(value));

                if (type == typeof(ulong?)) {
                    var id = Convert.ToUInt64(value);
                    if (property.Name.EndsWith("Channel") && guild.GetTextChannel(id) == null)
                        throw new ApplicationException(Messages.InvalidChannel);
                    if (property.Name.EndsWith("Role") && guild.GetRole(id) == null)
                        throw new ApplicationException(Messages.InvalidRole);

                    property.SetValue(config, id);
                }
            } catch (Exception e) when (e is FormatException or OverflowException) {
                throw new ApplicationException(Messages.InvalidSettingValue);
            }
        }
        config.Validate();

        await config.Save();
        await context.Channel.SendMessageAsync(Messages.SettingsUpdated);
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