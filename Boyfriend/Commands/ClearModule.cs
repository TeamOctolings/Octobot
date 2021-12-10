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
    [RequireBotPermission(GuildPermission.ManageMessages)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task Run(int toDelete) {
        if (Context.Channel is not ITextChannel channel) return;
        switch (toDelete) {
            case < 1:
                throw new ArgumentException("toDelete is less than 1.");
            case > 200:
                throw new ArgumentException("toDelete is more than 200.");
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