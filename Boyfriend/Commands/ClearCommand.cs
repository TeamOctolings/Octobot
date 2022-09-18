using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public sealed class ClearCommand : ICommand {
    public string[] Aliases { get; } = { "clear", "purge", "очистить", "стереть" };

    public async Task RunAsync(CommandProcessor cmd, string[] args, string[] cleanArgs) {
        if (cmd.Context.Channel is not SocketTextChannel channel) throw new Exception();

        if (!cmd.HasPermission(GuildPermission.ManageMessages)) return;

        var toDelete = cmd.GetNumberRange(cleanArgs, 0, 1, 200, "ClearAmount");
        if (toDelete == null) return;
        var messages = await channel.GetMessagesAsync((int)(toDelete + 1)).FlattenAsync();

        var user = (SocketGuildUser)cmd.Context.User;
        await channel.DeleteMessagesAsync(messages, Utils.GetRequestOptions(user.ToString()!));

        cmd.Audit(string.Format(Messages.FeedbackMessagesCleared, (toDelete + 1).ToString()));
    }
}
