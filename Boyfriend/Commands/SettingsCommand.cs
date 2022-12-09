using Discord;

namespace Boyfriend.Commands;

public sealed class SettingsCommand : ICommand {
    public string[] Aliases { get; } = { "settings", "config", "настройки", "конфиг" };

    public Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (!cmd.HasPermission(GuildPermission.ManageGuild)) return Task.CompletedTask;

        var guild = cmd.Context.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);

        if (args.Length is 0) {
            var currentSettings = Boyfriend.StringBuilder.AppendLine(Messages.CurrentSettings);

            foreach (var setting in Boyfriend.DefaultConfig) {
                var format = "{0}";
                var currentValue = config[setting.Key] is "default" ? Messages.DefaultWelcomeMessage : config[setting.Key];

                if (setting.Key.EndsWith("Channel")) {
                    if (guild.GetTextChannel(ulong.Parse(currentValue)) is not null) format = "<#{0}>";
                    else currentValue = Messages.ChannelNotSpecified;
                } else if (setting.Key.EndsWith("Role")) {
                    if (guild.GetRole(ulong.Parse(currentValue)) is not null) format = "<@&{0}>";
                    else currentValue = Messages.RoleNotSpecified;
                } else {
                    if (!IsBool(currentValue)) format = Utils.Wrap("{0}")!;
                    else currentValue = YesOrNo(currentValue is "true");
                }

                currentSettings.Append($"{Utils.GetMessage($"Settings{setting.Key}")} (`{setting.Key}`): ")
                    .AppendFormat(format, currentValue).AppendLine();
            }

            cmd.Reply(currentSettings.ToString(), ReplyEmojis.SettingsList);
            currentSettings.Clear();
            return Task.CompletedTask;
        }

        var selectedSetting = args[0].ToLower();

        var exists = false;
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        // Too many allocations
        foreach (var setting in Boyfriend.DefaultConfig.Keys) {
            if (selectedSetting != setting.ToLower()) continue;
            selectedSetting = setting;
            exists = true;
            break;
        }

        if (!exists) {
            cmd.Reply(Messages.SettingDoesntExist, ReplyEmojis.Error);
            return Task.CompletedTask;
        }

        string? value;

        if (args.Length >= 2) {
            value = cmd.GetRemaining(args, 1, "Setting");
            if (value is null) return Task.CompletedTask;
            if (selectedSetting is "EventStartedReceivers") {
                value = value.Replace(" ", "").ToLower();
                if (value.StartsWith(",") || value.Count(x => x is ',') > 1 ||
                    (!value.Contains("interested") && !value.Contains("users") && !value.Contains("role"))) {
                    cmd.Reply(Messages.InvalidSettingValue, ReplyEmojis.Error);
                    return Task.CompletedTask;
                }
            }
        } else { value = "reset"; }

        if (IsBool(Boyfriend.DefaultConfig[selectedSetting]) && !IsBool(value)) {
            value = value switch {
                "y" or "yes" or "д" or "да" => "true",
                "n" or "no" or "н" or "нет" => "false",
                _ => value
            };
            if (!IsBool(value)) {
                cmd.Reply(Messages.InvalidSettingValue, ReplyEmojis.Error);
                return Task.CompletedTask;
            }
        }

        var localizedSelectedSetting = Utils.GetMessage($"Settings{selectedSetting}");

        var mention = Utils.ParseMention(value);
        if (mention is not 0 && selectedSetting is not "WelcomeMessage") value = mention.ToString();

        var formatting = Utils.Wrap("{0}")!;
        if (selectedSetting is not "WelcomeMessage") {
            if (selectedSetting.EndsWith("Channel")) formatting = "<#{0}>";
            if (selectedSetting.EndsWith("Role")) formatting = "<@&{0}>";
        }

        var formattedValue = selectedSetting switch {
            "WelcomeMessage" => Utils.Wrap(Messages.DefaultWelcomeMessage),
            "EventStartedReceivers" => Utils.Wrap(Boyfriend.DefaultConfig[selectedSetting])!,
            _ => value is "reset" or "default" ? Messages.SettingNotDefined
                : IsBool(value) ? YesOrNo(value is "true")
                : string.Format(formatting, value)
        };

        if (value is "reset" or "default") {
            config[selectedSetting] = Boyfriend.DefaultConfig[selectedSetting];
        } else {
            if (value == config[selectedSetting]) {
                cmd.Reply(string.Format(Messages.SettingsNothingChanged, localizedSelectedSetting, formattedValue),
                    ReplyEmojis.Error);
                return Task.CompletedTask;
            }

            if (selectedSetting is "Lang" && !Utils.CultureInfoCache.ContainsKey(value)) {
                var langNotSupported = Boyfriend.StringBuilder.Append($"{Messages.LanguageNotSupported} ");
                foreach (var lang in Utils.CultureInfoCache) langNotSupported.Append($"`{lang.Key}`, ");
                langNotSupported.Remove(langNotSupported.Length - 2, 2);
                cmd.Reply(langNotSupported.ToString(), ReplyEmojis.Error);
                langNotSupported.Clear();
                return Task.CompletedTask;
            }

            if (selectedSetting.EndsWith("Channel") && guild.GetTextChannel(mention) is null) {
                cmd.Reply(Messages.InvalidChannel, ReplyEmojis.Error);
                return Task.CompletedTask;
            }

            if (selectedSetting.EndsWith("Role") && guild.GetRole(mention) is null) {
                cmd.Reply(Messages.InvalidRole, ReplyEmojis.Error);
                return Task.CompletedTask;
            }

            if (selectedSetting is "MuteRole") Utils.RemoveMuteRoleFromCache(ulong.Parse(config[selectedSetting]));

            config[selectedSetting] = value;
        }

        if (selectedSetting is "Lang") {
            Utils.SetCurrentLanguage(guild.Id);
            localizedSelectedSetting = Utils.GetMessage($"Settings{selectedSetting}");
        }

        cmd.ConfigWriteScheduled = true;

        var replyFormat = string.Format(Messages.FeedbackSettingsUpdated, localizedSelectedSetting, formattedValue);
        cmd.Reply(replyFormat, ReplyEmojis.SettingsSet);
        cmd.Audit(replyFormat, false);
        return Task.CompletedTask;
    }

    private static string YesOrNo(bool isYes) {
        return isYes ? Messages.Yes : Messages.No;
    }

    private static bool IsBool(string value) {
        return value is "true" or "false";
    }
}
