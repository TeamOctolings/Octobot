using System.Diagnostics;
using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class ClearCommand : ICommand {
    public string[] Aliases { get; } = { "clear", "purge", "очистить", "стереть" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (cmd.Context.Channel is not SocketTextChannel channel) throw new UnreachableException();

        if (!cmd.HasPermission(GuildPermission.ManageMessages)) return;

        var toDelete = cmd.GetNumberRange(cleanArgs, 0, 1, 200, "ClearAmount");
        if (toDelete is null) return;
        var messages = await channel.GetMessagesAsync((int)(toDelete + 1)).FlattenAsync();

        var user = (SocketGuildUser)cmd.Context.User;
        var msgArray = messages.ToArray();
        await channel.DeleteMessagesAsync(msgArray, Utils.GetRequestOptions(user.ToString()!));

        foreach (var msg in msgArray.Where(m => !m.Author.IsBot))
            cmd.Audit(
                string.Format(
                    Messages.CachedMessageDeleted, msg.Author.Mention,
                    Utils.MentionChannel(channel.Id),
                    Utils.Wrap(msg.CleanContent)));
    }
}
