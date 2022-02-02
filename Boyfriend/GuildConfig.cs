using System.Globalization;
using Newtonsoft.Json;

namespace Boyfriend;

public class GuildConfig {
    public ulong? Id { get; }
    public string? Lang { get; set; }
    public string? Prefix { get; set; }

    public bool? RemoveRolesOnMute { get; set; }
    public bool? UseSystemChannel { get; set; }
    public bool? SendWelcomeMessages { get; set; }
    public bool? ReceiveStartupMessages { get; set; }

    public string? WelcomeMessage { get; set; }

    public ulong? DefaultRole { get; set; }
    public ulong? MuteRole { get; set; }
    public ulong? AdminLogChannel { get; set; }
    public ulong? BotLogChannel { get; set; }

    public Dictionary<ulong, List<ulong>>? RolesRemovedOnMute { get; private set; }

    public GuildConfig(ulong id) {
        Id = id;
        Validate();
    }

    public void Validate() {
        if (Id == null) throw new Exception("Something went horribly, horribly wrong");

        Lang ??= "ru";
        Messages.Culture = new CultureInfo(Lang);
        Prefix ??= "!";
        RemoveRolesOnMute ??= false;
        UseSystemChannel ??= true;
        SendWelcomeMessages ??= true;
        ReceiveStartupMessages ??= true;
        WelcomeMessage ??= Messages.DefaultWelcomeMessage;
        DefaultRole ??= 0;
        MuteRole ??= 0;
        AdminLogChannel ??= 0;
        BotLogChannel ??= 0;
        RolesRemovedOnMute ??= new Dictionary<ulong, List<ulong>>();
    }

    public async Task Save() {
        Validate();
        RolesRemovedOnMute!.TrimExcess();

        await File.WriteAllTextAsync("config_" + Id + ".json", JsonConvert.SerializeObject(this));
    }
}
