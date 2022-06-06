using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class SettingsCommand : Command {
    public override string[] Aliases { get; } = { "settings", "config", "настройки", "конфиг" };
    public override int ArgsLengthRequired => 0;

    public override Task Run(SocketCommandContext context, string[] args) {
        var author = (SocketGuildUser)context.User;

        var permissionCheckResponse = CommandHandler.HasPermission(ref author, GuildPermission.ManageGuild);
        if (permissionCheckResponse != "") {
            Error(permissionCheckResponse, true);
            return Task.CompletedTask;
        }

        var guild = context.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);

        if (args.Length == 0) {
            var currentSettings = Boyfriend.StringBuilder.AppendLine(Messages.CurrentSettings);

            foreach (var setting in Boyfriend.DefaultConfig) {
                var format = "{0}";
                var currentValue = config[setting.Key];

                if (setting.Key.EndsWith("Channel")) {
                    if (guild.GetTextChannel(Convert.ToUInt64(currentValue)) != null)
                        format = "<#{0}>";
                    else
                        currentValue = Messages.ChannelNotSpecified;
                } else if (setting.Key.EndsWith("Role")) {
                    if (guild.GetRole(Convert.ToUInt64(currentValue)) != null)
                        format = "<@&{0}>";
                    else
                        currentValue = Messages.RoleNotSpecified;
                } else {
                    if (IsBool(currentValue))
                        currentValue = YesOrNo(currentValue == "true");
                    else
                        format = Utils.Wrap("{0}")!;
                }

                currentSettings.Append($"{Utils.GetMessage($"Settings{setting.Key}")} (`{setting.Key}`): ")
                    .AppendFormat(format, currentValue).AppendLine();
            }

            Output(ref currentSettings);
            currentSettings.Clear();
            return Task.CompletedTask;
        }

        var selectedSetting = args[0].ToLower();

        var exists = false;
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        // The performance impact is not worth it
        foreach (var setting in Boyfriend.DefaultConfig.Keys) {
            if (selectedSetting != setting.ToLower()) continue;
            selectedSetting = setting;
            exists = true;
            break;
        }

        if (!exists) {
            Error(Messages.SettingDoesntExist, false);
            return Task.CompletedTask;
        }

        string value;

        if (args.Length >= 2) {
            value = Utils.JoinString(ref args, 1);
            if (selectedSetting != "WelcomeMessage")
                value = value.Replace(" ", "").ToLower();
            if (value.StartsWith(",") || value.Count(x => x == ',') > 1) {
                Error(Messages.InvalidSettingValue, false);
                return Task.CompletedTask;
            }
        } else { value = "reset"; }

        if (IsBool(Boyfriend.DefaultConfig[selectedSetting]) && !IsBool(value)) {
            value = value switch {
                "y" or "yes" => "true",
                "n" or "no" => "false",
                _ => value
            };
            if (!IsBool(value)) {
                Error(Messages.InvalidSettingValue, false);
                return Task.CompletedTask;
            }
        }

        var localizedSelectedSetting = Utils.GetMessage($"Settings{selectedSetting}");

        var mention = Utils.ParseMention(value);
        if (mention != 0) value = mention.ToString();

        var formatting = Utils.Wrap("{0}")!;
        if (selectedSetting.EndsWith("Channel"))
            formatting = "<#{0}>";
        if (selectedSetting.EndsWith("Role"))
            formatting = "<@&{0}>";
        if (value is "0" or "reset" or "default")
            formatting = Messages.SettingNotDefined;
        var formattedValue = IsBool(value) ? YesOrNo(value == "true") : string.Format(formatting, value);

        if (value is "reset" or "default") {
            config[selectedSetting] = Boyfriend.DefaultConfig[selectedSetting];
        } else {
            if (value == config[selectedSetting]) {
                Error(string.Format(Messages.SettingsNothingChanged, localizedSelectedSetting, formattedValue), false);
                return Task.CompletedTask;
            }

            if (selectedSetting == "Lang" && value is not "ru" and not "en") {
                Error(Messages.LanguageNotSupported, false);
                return Task.CompletedTask;
            }

            if (selectedSetting.EndsWith("Channel") && guild.GetTextChannel(mention) == null) {
                Error(Messages.InvalidChannel, false);
                return Task.CompletedTask;
            }

            if (selectedSetting.EndsWith("Role") && guild.GetRole(mention) == null) {
                Error(Messages.InvalidRole, false);
                return Task.CompletedTask;
            }

            if (selectedSetting == "MuteRole") Utils.RemoveMuteRoleFromCache(ulong.Parse(config[selectedSetting]));

            config[selectedSetting] = value;
        }

        if (selectedSetting == "Lang") {
            Utils.SetCurrentLanguage(guild.Id);
            localizedSelectedSetting = Utils.GetMessage($"Settings{selectedSetting}");
        }

        CommandHandler.ConfigWriteScheduled = true;

        Success(string.Format(Messages.FeedbackSettingsUpdated, localizedSelectedSetting, formattedValue),
            author.Mention);
        return Task.CompletedTask;
    }

    private static string YesOrNo(bool isYes) {
        return isYes ? Messages.Yes : Messages.No;
    }

    private static bool IsBool(string value) {
        return value is "true" or "false";
    }
}