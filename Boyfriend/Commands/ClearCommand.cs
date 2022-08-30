using Discord;
using Discord.WebSocket;

namespace Boyfriend.Commands;

public class ClearCommand : Command {
    public override string[] Aliases { get; } = { "clear", "purge", "очистить", "стереть" };

    public override async Task Run(CommandProcessor cmd, string[] args) {
        if (cmd.Context.Channel is not SocketTextChannel channel) throw new Exception();

        if (!cmd.HasPermission(GuildPermission.ManageMessages)) return;

        var toDelete = cmd.GetNumberRange(args, 0, 1, 200, "ClearAmount");
        if (toDelete == null) return;
        var messages = await channel.GetMessagesAsync((int)(toDelete + 1)).FlattenAsync();

        var user = (SocketGuildUser)cmd.Context.User;
        await channel.DeleteMessagesAsync(messages, Utils.GetRequestOptions(user.ToString()!));

        cmd.Audit(string.Format(Messages.FeedbackMessagesCleared, (toDelete + 1).ToString()));
    }
}