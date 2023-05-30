namespace Boyfriend.Data;

public class GuildData {
    public readonly GuildConfiguration Configuration;
    public readonly string ConfigurationPath;

    public GuildData(GuildConfiguration configuration, string configurationPath) {
        Configuration = configuration;
        ConfigurationPath = configurationPath;
    }
}
