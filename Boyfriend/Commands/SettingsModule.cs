using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class SettingsModule : ModuleBase<SocketCommandContext> {

    [Command("settings")]
    [Summary("Настраивает бота")]
    [Alias("config", "настройки", "конфиг")]
    public async Task Run([Remainder] string s = "") {
        var config = Boyfriend.GetGuildConfig(Context.Guild);
        var sArray = s.Split(" ");
        if (s == "") {
            var nl = Environment.NewLine;
            await Context.Channel.SendMessageAsync($"Текущие настройки:{nl}Язык: `{config.Lang}`" +
                                                   $"{nl}Префикс: `{config.Prefix}`" +
                                                   $"{nl}Удалять роли при муте: " +
                                                   $"{(config.RemoveRolesOnMute ? "Да" : "Нет")}");
            return;
        }

        if (sArray[0].ToLower() == "lang") {
            if (sArray[1].ToLower() != "ru") throw new Exception("Язык не поддерживается!");
            config.Lang = sArray[1].ToLower();
        }


        if (sArray[0].ToLower() == "prefix")
            config.Prefix = sArray[1];

        if (sArray[0].ToLower() == "removerolesonmute") {
            try {
                config.RemoveRolesOnMute = bool.Parse(sArray[1].ToLower());
            } catch (FormatException) {
                await Context.Channel.SendMessageAsync("Неверный параметр! Требуется `true` или `false`");
                return;
            }
        }

        config.Save();

        await Context.Channel.SendMessageAsync("Настройки успешно обновлены!");
    }
}