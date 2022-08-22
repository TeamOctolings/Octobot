using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Humanizer;
using Humanizer.Localisation;

namespace Boyfriend;

public static class Utils {
    public static readonly Random Random = new();
    private static readonly Dictionary<string, string> ReflectionMessageCache = new();

    private static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "ru", new CultureInfo("ru-RU") },
        { "en", new CultureInfo("en-US") }
    };

    private static readonly Dictionary<ulong, SocketRole> MuteRoleCache = new();

    private static readonly AllowedMentions AllowRoles = new() {
        AllowedTypes = AllowedMentionTypes.Roles
    };

    public static string GetBeep(int i = -1) {
        return GetMessage($"Beep{(i < 0 ? Random.Next(3) + 1 : ++i)}");
    }

    public static SocketTextChannel? GetAdminLogChannel(ulong id) {
        return Boyfriend.Client.GetGuild(id)
            .GetTextChannel(ParseMention(Boyfriend.GetGuildConfig(id)["AdminLogChannel"]));
    }

    public static string? Wrap(string? original, bool limitedSpace = false) {
        if (original == null) return null;
        var maxChars = limitedSpace ? 970 : 1940;
        if (original.Length > maxChars) original = original[..maxChars];
        var style = original.Contains('\n') ? "```" : "`";
        return $"{style}{original}{(original.Equals("") ? " " : "")}{style}";
    }

    public static string MentionChannel(ulong id) {
        return $"<#{id}>";
    }

    public static ulong ParseMention(string mention) {
        return ulong.TryParse(Regex.Replace(mention, "[^0-9]", ""), out var id) ? id : 0;
    }

    public static SocketUser? ParseUser(string mention) {
        var user = Boyfriend.Client.GetUser(ParseMention(mention));
        return user;
    }

    public static SocketGuildUser? ParseMember(SocketGuild guild, string mention) {
        return guild.GetUser(ParseMention(mention));
    }

    public static async Task SendDirectMessage(SocketUser user, string toSend) {
        try { await user.SendMessageAsync(toSend); } catch (HttpException e) {
            if (e.DiscordCode != DiscordErrorCode.CannotSendMessageToUser)
                throw;
        }
    }

    public static SocketRole? GetMuteRole(ref SocketGuild guild) {
        var id = ulong.Parse(Boyfriend.GetGuildConfig(guild.Id)["MuteRole"]);
        if (MuteRoleCache.ContainsKey(id)) return MuteRoleCache[id];
        SocketRole? role = null;
        foreach (var x in guild.Roles) {
            if (x.Id != id) continue;
            role = x;
            MuteRoleCache.Add(id, role);
            break;
        }

        return role;
    }

    public static void RemoveMuteRoleFromCache(ulong id) {
        if (MuteRoleCache.ContainsKey(id)) MuteRoleCache.Remove(id);
    }

    public static async Task SilentSendAsync(SocketTextChannel? channel, string text, bool allowRoles = false) {
        if (channel == null || text.Length is 0 or > 2000)
            throw new Exception($"Message length is out of range: {text.Length}");

        await channel.SendMessageAsync(text, false, null, null, allowRoles ? AllowRoles : AllowedMentions.None);
    }

    public static TimeSpan? GetTimeSpan(ref string from) {
        var chars = from.AsSpan();
        var numberBuilder = Boyfriend.StringBuilder;
        int days = 0, hours = 0, minutes = 0, seconds = 0;
        foreach (var c in chars)
            if (char.IsDigit(c)) { numberBuilder.Append(c); } else {
                if (numberBuilder.Length == 0) return null;
                switch (c) {
                    case 'd' or 'D' or 'д' or 'Д':
                        days += int.Parse(numberBuilder.ToString());
                        numberBuilder.Clear();
                        break;
                    case 'h' or 'H' or 'ч' or 'Ч':
                        hours += int.Parse(numberBuilder.ToString());
                        numberBuilder.Clear();
                        break;
                    case 'm' or 'M' or 'м' or 'М':
                        minutes += int.Parse(numberBuilder.ToString());
                        numberBuilder.Clear();
                        break;
                    case 's' or 'S' or 'с' or 'С':
                        seconds += int.Parse(numberBuilder.ToString());
                        numberBuilder.Clear();
                        break;
                    default: return null;
                }
            }

        return new TimeSpan(days, hours, minutes, seconds);
    }

    public static string JoinString(ref string[] args, int startIndex) {
        return string.Join(" ", args, startIndex, args.Length - startIndex);
    }

    public static RequestOptions GetRequestOptions(string reason) {
        var options = RequestOptions.Default;
        options.AuditLogReason = reason;
        return options;
    }

    public static string GetMessage(string name) {
        var propertyName = name;
        name = $"{Messages.Culture}/{name}";
        if (ReflectionMessageCache.ContainsKey(name)) return ReflectionMessageCache[name];

        var toReturn =
            typeof(Messages).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?.ToString()! ?? throw new Exception($"Could not find localized property: {propertyName}");
        ReflectionMessageCache.Add(name, toReturn);
        return toReturn;
    }

    public static async Task SendFeedback(string feedback, ulong guildId, string mention, bool sendPublic = false) {
        var adminChannel = GetAdminLogChannel(guildId);
        var systemChannel = Boyfriend.Client.GetGuild(guildId).SystemChannel;
        var toSend = string.Format(Messages.FeedbackFormat, mention, feedback);
        if (adminChannel != null)
            await SilentSendAsync(adminChannel, toSend);
        if (sendPublic && systemChannel != null)
            await SilentSendAsync(systemChannel, toSend);
    }

    public static void StackFeedback(ref string feedback, ref string mention, bool isPublic) {
        var toAppend = string.Format(Messages.FeedbackFormat, mention, feedback);
        CommandHandler.StackedPrivateFeedback.AppendLine(toAppend);
        if (isPublic) CommandHandler.StackedPublicFeedback.AppendLine(toAppend);
    }

    public static string GetHumanizedTimeOffset(ref TimeSpan span) {
        return span.TotalSeconds > 0
            ? $" {span.Humanize(2, minUnit: TimeUnit.Second, maxUnit: TimeUnit.Month, culture: Messages.Culture)}"
            : Messages.Ever;
    }

    public static void SetCurrentLanguage(ulong guildId) {
        Messages.Culture = CultureInfoCache[Boyfriend.GetGuildConfig(guildId)["Lang"]];
    }
}