using System.Text.Json;

namespace Boyfriend;

public class GuildConfig {
    public ulong Id { get; }
    public string Lang { get; set; }
    public string Prefix { get; set; }
    public bool RemoveRolesOnMute { get; set; }

    public GuildConfig(ulong id, string lang, string prefix, bool removeRolesOnMute) {
        Id = id;
        Lang = lang;
        Prefix = prefix;
        RemoveRolesOnMute = removeRolesOnMute;
    }

    public async void Save() {
        await using var stream = File.OpenWrite("config_" + Id + ".json");
        await JsonSerializer.SerializeAsync(stream, this);
    }
}