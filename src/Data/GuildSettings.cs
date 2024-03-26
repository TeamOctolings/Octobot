using Octobot.Data.Options;
using Octobot.Responders;
using Remora.Discord.API.Abstractions.Objects;

namespace Octobot.Data;

/// <summary>
///     Contains all per-guild settings that can be set by a member
///     with <see cref="DiscordPermission.ManageGuild" /> using the /settings command
/// </summary>
public static class GuildSettings
{
    public static readonly LanguageOption Language = new("Language", "en");

    public static readonly Option<string> WarnPunishment = new("WarnPunishment", "disabled");

    /// <summary>
    ///     Controls what message should be sent in <see cref="PublicFeedbackChannel" /> when a new member joins the guild.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>No message will be sent if set to "off", "disable" or "disabled".</item>
    ///         <item><see cref="Messages.DefaultWelcomeMessage" /> will be sent if set to "default" or "reset".</item>
    ///     </list>
    /// </remarks>
    /// <seealso cref="GuildMemberJoinedResponder" />
    public static readonly Option<string> WelcomeMessage = new("WelcomeMessage", "default");

    /// <summary>
    ///     Controls what message should be sent in <see cref="PublicFeedbackChannel" /> when a member leaves the guild.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>No message will be sent if set to "off", "disable" or "disabled".</item>
    ///         <item><see cref="Messages.DefaultLeaveMessage" /> will be sent if set to "default" or "reset".</item>
    ///     </list>
    /// </remarks>
    /// <seealso cref="GuildMemberLeftResponder" />
    public static readonly Option<string> LeaveMessage = new("LeaveMessage", "default");

    /// <summary>
    ///     Controls whether or not the <see cref="Messages.Ready" /> message should be sent
    ///     in <see cref="PrivateFeedbackChannel" /> on startup.
    /// </summary>
    /// <seealso cref="GuildLoadedResponder" />
    public static readonly BoolOption ReceiveStartupMessages = new("ReceiveStartupMessages", false);

    public static readonly BoolOption RemoveRolesOnMute = new("RemoveRolesOnMute", false);

    /// <summary>
    ///     Controls whether or not a guild member's roles are returned if he/she leaves and then joins back.
    /// </summary>
    /// <remarks>Roles will not be returned if the member left the guild because of /ban or /kick.</remarks>
    public static readonly BoolOption ReturnRolesOnRejoin = new("ReturnRolesOnRejoin", false);

    public static readonly BoolOption AutoStartEvents = new("AutoStartEvents", false);

    /// <summary>
    ///     Controls whether or not users who try to hoist themselves should be renamed.
    /// </summary>
    public static readonly BoolOption RenameHoistedUsers = new("RenameHoistedUsers", false);

    public static readonly IntOption WarnsThreshold = new("WarnsThreshold", 0);

    /// <summary>
    ///     Controls what channel should all public messages be sent to.
    /// </summary>
    public static readonly SnowflakeOption PublicFeedbackChannel = new("PublicFeedbackChannel");

    /// <summary>
    ///     Controls what channel should all private, moderator-only messages be sent to.
    /// </summary>
    public static readonly SnowflakeOption PrivateFeedbackChannel = new("PrivateFeedbackChannel");

    /// <summary>
    ///     Controls what channel should welcome messages be sent to.
    /// </summary>
    public static readonly SnowflakeOption WelcomeMessagesChannel = new("WelcomeMessagesChannel");

    public static readonly SnowflakeOption EventNotificationChannel = new("EventNotificationChannel");
    public static readonly SnowflakeOption DefaultRole = new("DefaultRole");
    public static readonly SnowflakeOption MuteRole = new("MuteRole");
    public static readonly SnowflakeOption ModeratorRole = new("ModeratorRole");
    public static readonly SnowflakeOption EventNotificationRole = new("EventNotificationRole");

    /// <summary>
    ///     Controls the amount of time before a scheduled event to send a reminder in <see cref="EventNotificationChannel" />.
    /// </summary>
    public static readonly TimeSpanOption EventEarlyNotificationOffset = new(
        "EventEarlyNotificationOffset", TimeSpan.Zero);

    public static readonly TimeSpanOption WarnPunishmentDuration = new(
        "WarnPunishmentDuration", TimeSpan.Zero);
}
