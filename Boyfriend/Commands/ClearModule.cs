using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class ClearModule : ModuleBase<SocketCommandContext> {

    [Command("clear")]
    [Summary("Удаляет указанное количество сообщений")]
    [Alias("очистить")]
    public async Task Run(int toDelete) {
        if (Context.Channel is not ITextChannel channel) return;
        await CommandHandler.CheckPermissions(Context.Guild.GetUser(Context.User.Id), GuildPermission.ManageMessages);
        switch (toDelete) {
            case < 1:
                throw new Exception( "Указано отрицательное количество сообщений!");
            case > 200:
                throw new Exception("Указано слишком много сообщений!");
            default: {
                var messages = await channel.GetMessagesAsync(toDelete + 1).FlattenAsync();
                await channel.DeleteMessagesAsync(messages);
                await Utils.GetAdminLogChannel().SendMessageAsync(
                    $"{Context.User.Mention} удаляет {toDelete + 1} сообщений в канале " +
                    $"{Utils.MentionChannel(Context.Channel.Id)}");
                break;
            }
        }
    }
}