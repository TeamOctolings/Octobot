using System.Globalization;
using System.Text.RegularExpressions;
using Discord;
using Discord.Net;

namespace Boyfriend;

public static class Utils {
    private static readonly string[] Formats = {
        "%d'd'%h'h'%m'm'%s's'", "%d'd'%h'h'%m'm'", "%d'd'%h'h'%s's'", "%d'd'%h'h'", "%d'd'%m'm'%s's'", "%d'd'%m'm'",
        "%d'd'%s's'", "%d'd'", "%h'h'%m'm'%s's'", "%h'h'%m'm'", "%h'h'%s's'", "%h'h'", "%m'm'%s's'", "%m'm'", "%s's'",

        "%d'д'%h'ч'%m'м'%s'с'", "%d'д'%h'ч'%m'м'", "%d'д'%h'ч'%s'с'", "%d'д'%h'ч'", "%d'д'%m'м'%s'с'", "%d'д'%m'м'",
        "%d'д'%s'с'", "%d'д'", "%h'ч'%m'м'%s'с'", "%h'ч'%m'м'", "%h'ч'%s'с'", "%h'ч'", "%m'м'%s'с'", "%m'м'", "%s'с'"
    };

    public static string GetBeep(string cultureInfo, int i = -1) {
        Messages.Culture = new CultureInfo(cultureInfo);

        var beeps = new[] {Messages.Beep1, Messages.Beep2, Messages.Beep3};
        return beeps[i < 0 ? new Random().Next(3) : i];
    }

    public static async Task<ITextChannel?> GetAdminLogChannel(IGuild guild) {
        var adminLogChannel = await ParseChannelNullable(Boyfriend.GetGuildConfig(guild).AdminLogChannel.ToString()!);
        return adminLogChannel as ITextChannel;
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

    private static ulong ParseMention(string mention) {
        return Convert.ToUInt64(Regex.Replace(mention, "[^0-9]", ""));
    }

    private static ulong? ParseMentionNullable(string mention) {
        try {
            return ParseMention(mention) == 0 ? throw new FormatException() : ParseMention(mention);
        } catch (FormatException) {
            return null;
        }
    }

    public static async Task<IUser> ParseUser(string mention) {
        var user = Boyfriend.Client.GetUserAsync(ParseMention(mention));
        return await user;
    }

    public static async Task<IGuildUser> ParseMember(IGuild guild, string mention) {
        return await guild.GetUserAsync(ParseMention(mention));
    }

    private static async Task<IChannel> ParseChannel(string mention) {
        return await Boyfriend.Client.GetChannelAsync(ParseMention(mention));
    }

    public static async Task<IChannel?> ParseChannelNullable(string mention) {
        return ParseMentionNullable(mention) == null ? null : await ParseChannel(mention);
    }

    public static IRole? ParseRole(IGuild guild, string mention) {
        return guild.GetRole(ParseMention(mention));
    }

    public static IRole? ParseRoleNullable(IGuild guild, string mention) {
        return ParseMentionNullable(mention) == null ? null : ParseRole(guild, mention);
    }

    public static async Task SendDirectMessage(IUser user, string toSend) {
        try {
            await user.SendMessageAsync(toSend);
        } catch (HttpException e) {
            if (e.DiscordCode != DiscordErrorCode.CannotSendMessageToUser)
                throw;
        }
    }

    public static IRole? GetMuteRole(IGuild guild) {
        var role = guild.Roles.FirstOrDefault(x => x.Id == Boyfriend.GetGuildConfig(guild).MuteRole);
        return role;
    }

    public static async Task SilentSendAsync(ITextChannel? channel, string text) {
        if (channel == null) return;

        try {
            await channel.SendMessageAsync(text, false, null, null, AllowedMentions.None);
        } catch (ArgumentException) {}
    }
    public static TimeSpan GetTimeSpan(string from) {
        return TimeSpan.ParseExact(from.ToLowerInvariant(), Formats,
            CultureInfo.InvariantCulture);
    }

    public static string JoinString(string[] args, int startIndex) {
        return string.Join(" ", args, startIndex, args.Length - startIndex);
    }

    public static string GetNameAndDiscrim(IUser user) {
        return $"{user.Username}#{user.Discriminator}";
    }

    public static RequestOptions GetRequestOptions(string reason) {
        var options = RequestOptions.Default;
        options.AuditLogReason = reason;
        return options;
    }
}
