using System.Text.RegularExpressions;
using Discord;
using Discord.Net;

namespace Boyfriend;

public static class Utils {
    public static string GetBeep() {
        var letters = new[] { "а", "о", "и"};
        return $"Б{letters[new Random().Next(3)]}п! ";
    }

    [Obsolete("Stop hard-coding things!")]
    public static ITextChannel GetAdminLogChannel() {
        if (Boyfriend.Client.GetChannel(870929165141032971) is not ITextChannel adminLogChannel)
            throw new Exception("Invalid admin log channel");
        return adminLogChannel;
    }

    public static string Wrap(string original) {
        var toReturn = original.Replace("```", "​`​`​`​");
        return $"```{toReturn}{(toReturn.EndsWith("`") || toReturn.Trim().Equals("") ? " " : "")}```";
    }

    public static string WrapInline(string original) {
        return $"`{original}`";
    }

    public static string MentionChannel(ulong id) {
        return $"<#{id}>";
    }

    public static async Task StartDelayed(Task toRun, TimeSpan delay, Func<bool>? condition = null) {
        await Task.Delay(delay);
        var conditionResult = condition?.Invoke() ?? true;
        if (conditionResult)
            toRun.Start();
    }

    private static ulong ParseMention(string mention) {
        return Convert.ToUInt64(Regex.Replace(mention, "[^0-9]", ""));
    }

    public static async Task<IUser> ParseUser(string mention) {
        var user = Boyfriend.Client.GetUserAsync(ParseMention(mention));
        return await user;
    }

    public static async Task<IGuildUser> ParseMember(IGuild guild, string mention) {
        return await guild.GetUserAsync(ParseMention(mention));
    }

    public static async Task SendDirectMessage(IUser user, string toSend) {
        try {
            await user.SendMessageAsync(toSend);
        } catch (HttpException e) {
            if (e.DiscordCode != DiscordErrorCode.CannotSendMessageToUser)
                throw;
        }
    }

    public static IRole GetMuteRole(IGuild guild) {
        var role = guild.Roles.FirstOrDefault(x => x.Name.ToLower() is "заключённый" or "muted");
        if (role == null) throw new Exception("Не удалось найти роль мута");
        return role;
    }

    public static async Task SilentSendAsync(ITextChannel channel, string text) {
        await channel.SendMessageAsync(text, false, null, null, AllowedMentions.None);
    }
}