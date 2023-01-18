using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Boyfriend.Data;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Humanizer;
using Humanizer.Localisation;

namespace Boyfriend;

public static partial class Utils {
    public static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "ru", new CultureInfo("ru-RU") },
        { "en", new CultureInfo("en-US") },
        { "mctaylors-ru", new CultureInfo("tt-RU") }
    };

    private static readonly Dictionary<string, string> ReflectionMessageCache = new();

    private static readonly AllowedMentions AllowRoles = new() {
        AllowedTypes = AllowedMentionTypes.Roles
    };

    public static string GetBeep(int i = -1) {
        return GetMessage($"Beep{(i < 0 ? Random.Shared.Next(3) + 1 : ++i)}");
    }

    public static string? Wrap(string? original, bool limitedSpace = false) {
        if (original is null) return null;
        var maxChars = limitedSpace ? 970 : 1940;
        if (original.Length > maxChars) original = original[..maxChars];
        var style = original.Contains('\n') ? "```" : "`";
        return $"{style}{original}{(original.Equals("") ? " " : "")}{style}";
    }

    public static string MentionChannel(ulong id) {
        return $"<#{id}>";
    }

    public static ulong ParseMention(string mention) {
        return ulong.TryParse(NumbersOnlyRegex().Replace(mention, ""), out var id) ? id : 0;
    }

    public static async Task SendDirectMessage(SocketUser user, string toSend) {
        try { await user.SendMessageAsync(toSend); } catch (HttpException e) {
            if (e.DiscordCode is not DiscordErrorCode.CannotSendMessageToUser) throw;
        }
    }

    public static async Task SilentSendAsync(SocketTextChannel? channel, string text, bool allowRoles = false) {
        try {
            if (channel is null || text.Length is 0 or > 2000)
                throw new UnreachableException($"Message length is out of range: {text.Length}");

            await channel.SendMessageAsync(text, false, null, null, allowRoles ? AllowRoles : AllowedMentions.None);
        } catch (Exception e) {
            await Boyfriend.Log(new LogMessage(LogSeverity.Error, nameof(Utils),
                "Exception while silently sending message", e));
        }
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
        if (toReturn is null) {
            Console.Error.WriteLine($@"Could not find localized property: {propertyName}");
            return name;
        }

        ReflectionMessageCache.Add(name, toReturn);
        return toReturn;
    }

    public static async Task
        SendFeedbackAsync(string feedback, SocketGuild guild, string mention, bool sendPublic = false) {
        var data = GuildData.Get(guild);
        var adminChannel = data.PrivateFeedbackChannel;
        var systemChannel = data.PublicFeedbackChannel;
        var toSend = $"*[{mention}: {feedback}]*";
        if (adminChannel is not null) await SilentSendAsync(adminChannel, toSend);
        if (sendPublic && systemChannel is not null) await SilentSendAsync(systemChannel, toSend);
    }

    public static string GetHumanizedTimeSpan(TimeSpan span) {
        return span.TotalSeconds < 1
            ? Messages.Ever
            : $" {span.Humanize(2, minUnit: TimeUnit.Second, maxUnit: TimeUnit.Month, culture: Messages.Culture.Name.Contains("RU") ? CultureInfoCache["ru"] : Messages.Culture)}";
    }

    public static void SetCurrentLanguage(SocketGuild guild) {
        Messages.Culture = CultureInfoCache[GuildData.Get(guild).Preferences["Lang"]];
    }

    public static void SafeAppendToBuilder(StringBuilder appendTo, string appendWhat, SocketTextChannel? channel) {
        if (channel is null) return;
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

    public static SocketTextChannel? GetEventNotificationChannel(SocketGuild guild) {
        return guild.GetTextChannel(ParseMention(GuildData.Get(guild)
            .Preferences["EventNotificationChannel"]));
    }

    public static bool UserExists(ulong id) {
        return Boyfriend.Client.GetUser(id) is not null || UserInMemberData(id);
    }

    private static bool UserInMemberData(ulong id) {
        return GuildData.GuildDataDictionary.Values.Any(gData => gData.MemberData.Values.Any(mData => mData.Id == id));
    }

    public static async Task<bool> UnmuteMemberAsync(GuildData data, string modDiscrim, SocketGuildUser toUnmute,
        string reason) {
        var requestOptions = GetRequestOptions($"({modDiscrim}) {reason}");
        var role = data.MuteRole;

        if (role is not null) {
            if (!toUnmute.Roles.Contains(role)) return false;
            if (data.Preferences["RemoveRolesOnMute"] is "true")
                await toUnmute.AddRolesAsync(data.MemberData[toUnmute.Id].Roles, requestOptions);
            await toUnmute.RemoveRoleAsync(role, requestOptions);
            data.MemberData[toUnmute.Id].MutedUntil = null;
        } else {
            if (toUnmute.TimedOutUntil is null || toUnmute.TimedOutUntil.Value < DateTimeOffset.Now) return false;

            await toUnmute.RemoveTimeOutAsync(requestOptions);
        }

        return true;
    }

    public static async Task ReturnRolesAsync(SocketGuildUser user, List<ulong> roles) {
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var role in roles)
            if (role != user.Guild.Id)
                await user.AddRoleAsync(role);
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NumbersOnlyRegex();
}
