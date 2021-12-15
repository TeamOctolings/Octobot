using System.Text.Json;

namespace Boyfriend;

public class GuildConfig {
    public ulong Id { get; }
    public string Lang { get; set; }
    public string Prefix { get; set; }
    public bool RemoveRolesOnMute { get; set; }
    public bool UseSystemChannel { get; set; }
    public bool SendWelcomeMessages { get; set; }
    public ulong MuteRole { get; set; }
    public ulong AdminLogChannel { get; set; }
    public ulong BotLogChannel { get; set; }
    public Dictionary<ulong, List<ulong>> RolesRemovedOnMute { get; set; }

    public GuildConfig(ulong id, string lang, string prefix, bool removeRolesOnMute, bool useSystemChannel,
        bool sendWelcomeMessages, ulong muteRole, ulong adminLogChannel, ulong botLogChannel) {
        Id = id;
        Lang = lang;
        Prefix = prefix;
        RemoveRolesOnMute = removeRolesOnMute;
        UseSystemChannel = useSystemChannel;
        SendWelcomeMessages = sendWelcomeMessages;
        MuteRole = muteRole;
        AdminLogChannel = adminLogChannel;
        BotLogChannel = botLogChannel;
        RolesRemovedOnMute = new Dictionary<ulong, List<ulong>>();
    }

    public async void Save() {
        await using var stream = File.OpenWrite("config_" + Id + ".json");
        await JsonSerializer.SerializeAsync(stream, this);
    }
}