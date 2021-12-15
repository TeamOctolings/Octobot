using Discord;
using Discord.Commands;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

public class SettingsModule : ModuleBase<SocketCommandContext> {

    [Command("settings")]
    [Summary("Настраивает бота")]
    [Alias("config", "настройки", "конфиг")]
    public async Task Run([Remainder] string s = "") {
        await CommandHandler.CheckPermissions(Context.Guild.GetUser(Context.User.Id), GuildPermission.ManageGuild);
        var config = Boyfriend.GetGuildConfig(Context.Guild);
        var sArray = s.Split(" ");
        var guild = Context.Guild;
        if (s == "") {
            var nl = Environment.NewLine;
            var adminLogChannel = guild.GetTextChannel(config.AdminLogChannel);
            var admin = adminLogChannel == null ? "Не указан" : adminLogChannel.Mention;
            var botLogChannel = guild.GetTextChannel(config.BotLogChannel);
            var bot = botLogChannel == null ? "Не указан" : botLogChannel.Mention;
            var muteRole = guild.GetRole(config.MuteRole);
            var mute = muteRole == null ? "Не указана" : muteRole.Mention;
            var toSend = $"Текущие настройки:{nl}" +
                         $"Язык (`lang`): `{config.Lang}`{nl}" +
                         $"Префикс (`prefix`): `{config.Prefix}`{nl}" +
                         $"Удалять роли при муте (`removeRolesOnMute`): {YesOrNo(config.RemoveRolesOnMute)}{nl}" +
                         "Использовать канал системных сообщений для уведомлений (`useSystemChannel`): " +
                         $"{YesOrNo(config.UseSystemChannel)}{nl}" +
                         $"Отправлять приветствия (`sendWelcomeMessages`): {YesOrNo(config.UseSystemChannel)}{nl}" +
                         $"Роль мута (`muteRole`): {mute}{nl}" +
                         $"Канал админ-уведомлений (`adminLogChannel`): " +
                         $"{admin}{nl}" +
                         $"Канал бот-уведомлений (`botLogChannel`): " +
                         $"{bot}";
            await Utils.SilentSendAsync(Context.Channel as ITextChannel ?? throw new Exception(), toSend);
            return;
        }

        var setting = sArray[0].ToLower();
        var value = sArray[1].ToLower();

        ITextChannel? channel;
        try {
            channel = await Utils.ParseChannel(value) as ITextChannel;
        } catch (FormatException) {
            channel = null;
        }

        IRole? role;
        try {
            role = Utils.ParseRole(guild, value);
        }
        catch (FormatException) {
            role = null;
        }

        var boolValue = ParseBool(sArray[1]);

        switch (setting) {
            case "lang" when sArray[1].ToLower() != "ru":
                throw new Exception("Язык не поддерживается!");
            case "lang":
                config.Lang = value;
                break;
            case "prefix":
                config.Prefix = value;
                break;
            case "removerolesonmute":
                config.RemoveRolesOnMute = boolValue ??
                                           throw new Exception("Неверный параметр! Требуется `true` или `false");
                break;
            case "usesystemchannel":
                config.UseSystemChannel = boolValue ??
                                          throw new Exception("Неверный параметр! Требуется `true` или `false");
                break;
            case "sendwelcomemessages":
                config.SendWelcomeMessages = boolValue ??
                                             throw new Exception("Неверный параметр! Требуется `true` или `false");
                break;
            case "adminlogchannel":
                config.AdminLogChannel = Convert.ToUInt64((channel ??
                                                           throw new Exception("Указан недействительный канал!"))
                    .Id);
                break;
            case "botlogchannel":
                config.BotLogChannel = Convert.ToUInt64((channel ??
                                                           throw new Exception("Указан недействительный канал!"))
                    .Id);
                break;
            case "muterole":
                config.MuteRole = Convert.ToUInt64((role ?? throw new Exception("Указана недействительная роль!"))
                    .Id);
                break;
        }

        config.Save();

        await Context.Channel.SendMessageAsync("Настройки успешно обновлены!");
    }

    private static bool? ParseBool(string toParse) {
        try {
            return bool.Parse(toParse.ToLower());
        } catch (FormatException) {
            return null;
        }
    }

    private static string YesOrNo(bool isYes) {
        return isYes ? "Да" : "Нет";
    }
}