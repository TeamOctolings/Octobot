using Discord;
using Discord.Commands;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class BanModule : ModuleBase<SocketCommandContext> {

    [Command("ban")]
    [Summary("Банит пользователя")]
    [Alias("бан")]
    [RequireBotPermission(GuildPermission.BanMembers)]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public Task Run(string user, TimeSpan duration, [Remainder]string reason) {
        var toBan = Utils.ParseUser(user).Result;
        BanUser(Context.Guild, Context.User, toBan, duration, reason);
        return Task.CompletedTask;
    }

    public static async void BanUser(IGuild guild, IUser author, IUser toBan, TimeSpan duration, string reason) {
        var authorMention = author.Mention;
        await Utils.SendDirectMessage(toBan, $"Тебя забанил {author.Mention} на сервере {guild.Name} за `{reason}`");

        var guildBanMessage = $"({author.Username}#{author.Discriminator}) {reason}";
        await guild.AddBanAsync(toBan, 0, guildBanMessage);
        var notification = $"{authorMention} банит {toBan.Mention} за {Utils.WrapInline(reason)}";
        await Utils.SilentSendAsync(guild.GetSystemChannelAsync().Result, notification);
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), notification);
        var task = new Task(() => UnbanModule.UnbanUser(guild, guild.GetCurrentUserAsync().Result, toBan,
            "Время наказания истекло"));
        await Utils.StartDelayed(task, duration, () => guild.GetBanAsync(toBan).Result != null);
    }
}