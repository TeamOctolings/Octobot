using System.Globalization;

namespace Boyfriend.Data;

public class GuildConfiguration {
    public static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "en", new CultureInfo("en-US") },
        { "ru", new CultureInfo("ru-RU") },
        { "mctaylors-ru", new CultureInfo("tt-RU") }
    };

    public string Prefix                   { get; set; } = "!";
    public string Language                 { get; set; } = "en";
    public string WelcomeMessage           { get; set; } = "default";
    public bool   ReceiveStartupMessages   { get; set; }
    public bool   RemoveRolesOnMute        { get; set; }
    public bool   ReturnRolesOnRejoin      { get; set; }
    public bool   AutoStartEvents          { get; set; }
    public ulong  PublicFeedbackChannel    { get; set; } = 0;
    public ulong  PrivateFeedbackChannel   { get; set; } = 0;
    public ulong  EventNotificationChannel { get; set; } = 0;
    public ulong  StarterRole              { get; set; } = 0;
    public ulong  MuteRole                 { get; set; } = 0;
    public ulong  EventNotificationRole    { get; set; } = 0;

    public List<NotificationReceiver> EventStartedReceivers { get; set; }
        = new() { NotificationReceiver.Interested, NotificationReceiver.Role };

    public TimeSpan EventEarlyNotificationOffset { get; set; } = TimeSpan.Zero;

    public CultureInfo Culture => CultureInfoCache[Language];
}
