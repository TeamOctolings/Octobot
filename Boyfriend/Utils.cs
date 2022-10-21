using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Boyfriend.Commands;
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

    public static async Task SendDirectMessage(SocketUser user, string toSend) {
        try { await user.SendMessageAsync(toSend); } catch (HttpException e) {
            if (e.DiscordCode != DiscordErrorCode.CannotSendMessageToUser)
                throw;
        }
    }

    public static SocketRole? GetMuteRole(SocketGuild guild) {
        var id = ulong.Parse(Boyfriend.GetGuildConfig(guild.Id)["MuteRole"]);
        if (MuteRoleCache.TryGetValue(id, out var cachedMuteRole)) return cachedMuteRole;
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

    public static RequestOptions GetRequestOptions(string reason) {
        var options = RequestOptions.Default;
        options.AuditLogReason = reason;
        return options;
    }

    public static string GetMessage(string name) {
        var propertyName = name;
        name = $"{Messages.Culture}/{name}";
        if (ReflectionMessageCache.TryGetValue(name, out var cachedMessage)) return cachedMessage;

        var toReturn =
            typeof(Messages).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?.ToString();
        if (toReturn == null) {
            Console.WriteLine($@"Could not find localized property: {propertyName}");
            return name;
        }

        ReflectionMessageCache.Add(name, toReturn);
        return toReturn;
    }

    public static async Task
        SendFeedbackAsync(string feedback, ulong guildId, string mention, bool sendPublic = false) {
        var adminChannel = GetAdminLogChannel(guildId);
        var systemChannel = Boyfriend.Client.GetGuild(guildId).SystemChannel;
        var toSend = string.Format(Messages.FeedbackFormat, mention, feedback);
        if (adminChannel != null)
            await SilentSendAsync(adminChannel, toSend);
        if (sendPublic && systemChannel != null)
            await SilentSendAsync(systemChannel, toSend);
    }

    public static string GetHumanizedTimeOffset(TimeSpan span) {
        return span.TotalSeconds > 0
            ? $" {span.Humanize(2, minUnit: TimeUnit.Second, maxUnit: TimeUnit.Month, culture: Messages.Culture)}"
            : Messages.Ever;
    }

    public static void SetCurrentLanguage(ulong guildId) {
        Messages.Culture = CultureInfoCache[Boyfriend.GetGuildConfig(guildId)["Lang"]];
    }

    public static void SafeAppendToBuilder(StringBuilder appendTo, string appendWhat, SocketTextChannel? channel) {
        if (channel == null) return;
        if (appendTo.Length + appendWhat.Length > 2000) {
            _ = SilentSendAsync(channel, appendTo.ToString());
            appendTo.Clear();
        }

        appendTo.AppendLine(appendWhat);
    }

    public static void SafeAppendToBuilder(StringBuilder appendTo, string appendWhat, SocketUserMessage message) {
        if (appendTo.Length + appendWhat.Length > 2000) {
            _ = message.ReplyAsync(appendTo.ToString(), false, null, AllowedMentions.None);
            appendTo.Clear();
        }

        appendTo.AppendLine(appendWhat);
    }

    public static async Task DelayedUnbanAsync(CommandProcessor cmd, ulong banned, string reason, TimeSpan duration) {
        await Task.Delay(duration);
        await UnbanCommand.UnbanUserAsync(cmd, banned, reason);
    }

    public static async Task DelayedUnmuteAsync(CommandProcessor cmd, SocketGuildUser muted, string reason,
        TimeSpan duration) {
        await Task.Delay(duration);
        await UnmuteCommand.UnmuteMemberAsync(cmd, muted, reason);
    }

    public static bool IsServerBlacklisted(SocketGuild guild) {
        return guild.GetUser(196160375593369600) != null && guild.OwnerId != 326642240229474304 &&
               guild.OwnerId != 504343489664909322;
    }
}
