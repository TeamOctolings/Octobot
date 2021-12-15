using System.Globalization;
using System.Text.RegularExpressions;
using Discord;
using Discord.Net;

namespace Boyfriend;

public static class Utils {
    public static string GetBeep() {
        var letters = new[] {"а", "о", "и"};
        return $"Б{letters[new Random().Next(3)]}п! ";
    }

    public static async Task<ITextChannel> GetAdminLogChannel(IGuild guild) {
        var adminLogChannel = await ParseChannel(Boyfriend.GetGuildConfig(guild).AdminLogChannel.ToString());
        if (adminLogChannel is ITextChannel channel)
            return channel;

        throw new Exception("Неверный канал админ-логов для гильдии " + guild.Id);
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

    public static async Task<IChannel> ParseChannel(string mention) {
        return await Boyfriend.Client.GetChannelAsync(ParseMention(mention));
    }

    public static IRole ParseRole(IGuild guild, string mention) {
        return guild.GetRole(ParseMention(mention));
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
        var role = guild.Roles.FirstOrDefault(x => x.Id == Boyfriend.GetGuildConfig(guild).MuteRole);
        if (role == null) throw new Exception("Требуется указать роль мута в настройках!");
        return role;
    }

    public static async Task SilentSendAsync(ITextChannel channel, string text) {
        try {
            await channel.SendMessageAsync(text, false, null, null, AllowedMentions.None);
        } catch (ArgumentException) {}
    }

    private static readonly string[] Formats = {
        "%d'd'%h'h'%m'm'%s's'", "%d'd'%h'h'%m'm'", "%d'd'%h'h'%s's'", "%d'd'%h'h'", "%d'd'%m'm'%s's'", "%d'd'%m'm'",
        "%d'd'%s's'", "%d'd'", "%h'h'%m'm'%s's'", "%h'h'%m'm'", "%h'h'%s's'", "%h'h'", "%m'm'%s's'", "%m'm'", "%s's'"
    };
    public static TimeSpan GetTimeSpan(string from) {
        return TimeSpan.ParseExact(from.ToLowerInvariant(), Formats,
            CultureInfo.InvariantCulture);
    }
}