using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class KickModule : ModuleBase<SocketCommandContext> {

    [Command("kick")]
    [Summary("Выгоняет пользователя")]
    [Alias("кик")]
    public async Task Run(string user, [Remainder]string reason) {
        var author = Context.Guild.GetUser(Context.User.Id);
        var toKick = Utils.ParseMember(Context.Guild, user).Result;
        await CommandHandler.CheckPermissions(author, GuildPermission.KickMembers);
        await CommandHandler.CheckInteractions(author, toKick);
        KickMember(Context.Guild, Context.Guild.GetUser(Context.User.Id), toKick, reason);
    }

    private static async void KickMember(IGuild guild, IGuildUser author, IGuildUser toKick, string reason) {
        var authorMention = author.Mention;
        await Utils.SendDirectMessage(toKick, $"Тебя кикнул {authorMention} на сервере {guild.Name} за " +
                                              $"{Utils.WrapInline(reason)}");

        var guildKickMessage = $"({author.Username}#{author.Discriminator}) {reason}";
        await toKick.KickAsync(guildKickMessage);
        var notification = $"{authorMention} выгоняет {toKick.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
    }
}