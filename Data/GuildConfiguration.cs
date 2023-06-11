using System.Globalization;
using Remora.Discord.API.Abstractions.Objects;

namespace Boyfriend.Data;

/// <summary>
///     Stores per-guild settings that can be set by a member
///     with <see cref="DiscordPermission.ManageGuild" /> using the /settings command
/// </summary>
public class GuildConfiguration {
    /// <summary>
    ///     Represents a scheduled event notification receiver.
    /// </summary>
    /// <remarks>
    ///     Used to selectively mention guild members when a scheduled event has started or is about to start.
    /// </remarks>
    public enum NotificationReceiver {
        Interested,
        Role
    }

    public static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "en", new CultureInfo("en-US") },
        { "ru", new CultureInfo("ru-RU") },
        { "mctaylors-ru", new CultureInfo("tt-RU") }
    };

    public string Language { get; set; } = "en";

    /// <summary>
    ///     Controls what message should be sent in <see cref="PublicFeedbackChannel" /> when a new member joins the server.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>No message will be sent if set to "off", "disable" or "disabled".</item>
    ///         <item><see cref="Messages.DefaultWelcomeMessage" /> will be sent if set to "default" or "reset"</item>
    ///     </list>
    /// </remarks>
    /// <seealso cref="GuildMemberAddResponder" />
    public string WelcomeMessage { get; set; } = "default";

    /// <summary>
    ///     Controls whether or not the <see cref="Messages.Ready" /> message should be sent
    ///     in <see cref="PrivateFeedbackChannel" /> on startup.
    /// </summary>
    /// <seealso cref="GuildCreateResponder" />
    public bool ReceiveStartupMessages { get; set; }

    public bool RemoveRolesOnMute { get; set; }

    /// <summary>
    ///     Controls whether or not a guild member's roles are returned if he/she leaves and then joins back.
    /// </summary>
    /// <remarks>Roles will not be returned if the member left the guild because of /ban or /kick.</remarks>
    public bool ReturnRolesOnRejoin { get; set; }

    public bool AutoStartEvents { get; set; }

    /// <summary>
    ///     Controls what channel should all public messages be sent to.
    /// </summary>
    public ulong PublicFeedbackChannel { get; set; }

    /// <summary>
    ///     Controls what channel should all private, moderator-only messages be sent to.
    /// </summary>
    public ulong PrivateFeedbackChannel { get; set; }

    public ulong EventNotificationChannel { get; set; }
    public ulong DefaultRole              { get; set; }
    public ulong MuteRole                 { get; set; }
    public ulong EventNotificationRole    { get; set; }

    /// <summary>
    ///     Controls what guild members should be mentioned when a scheduled event has started or is about to start.
    /// </summary>
    /// <seealso cref="NotificationReceiver" />
    public List<NotificationReceiver> EventStartedReceivers { get; set; }
        = new() { NotificationReceiver.Interested, NotificationReceiver.Role };

    /// <summary>
    ///     Controls the amount of time before a scheduled event to send a reminder in <see cref="EventNotificationChannel" />.
    /// </summary>
    public TimeSpan EventEarlyNotificationOffset { get; set; } = TimeSpan.Zero;

    public CultureInfo GetCulture() {
        return CultureInfoCache[Language];
    }
}
