using System.Text;
using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;

namespace Octobot.Services.Update;

public sealed class SongUpdateService : BackgroundService
{
    private static readonly (string Name, string Author, TimeSpan Duration)[] SongList =
    {
        ("Rockagilly Blues", "Yoko & the Gold Bazookas", new TimeSpan(0, 3, 37)),
        ("Seep and Destroy", "Splatoon 3", new TimeSpan(0, 2, 42)),
        ("Big Betrayal", "Deep Cut", new TimeSpan(0, 1, 42)),
        ("Tomorrow's Nostalgia Today", "Squid Sisters", new TimeSpan(0, 2, 8)),
        ("Anarchy Rainbow", "Deep Cut", new TimeSpan(0, 1, 51)),
        ("Liquid Sunshine", "Squid Sisters feat. Ian BGM", new TimeSpan(0, 1, 32)),
        ("Candy-Coated Rocks", "Damp Socks feat. Off the Hook", new TimeSpan(0, 1, 11)),
        ("Aquasonic", "H2Whoa", new TimeSpan(0, 1, 1)),
        ("Ska-Blam!", "Yoko & the Gold Bazookas", new TimeSpan(0, 4, 4)),
        ("Muck Warfare", "Off the Hook", new TimeSpan(0, 3, 39)),
        ("Acid Hues", "Off the Hook", new TimeSpan(0, 3, 39)),
        ("Shark Bytes", "Off the Hook", new TimeSpan(0, 3, 48)),
        ("Calamari Inkantation 3MIX", "DJ Octavio feat. Squid Sisters & Deep Cut", new TimeSpan(0, 7, 9)),
        ("Ink Me Up", "Squid Sisters", new TimeSpan(0, 2, 13))
    };

    private readonly List<Activity> _activityList = new(1)
    {
        new Activity("with Remora.Discord", ActivityType.Game)
    };

    private readonly DiscordGatewayClient _client;
    private readonly GuildDataService _guildData;

    private uint _nextSongIndex;

    public SongUpdateService(DiscordGatewayClient client, GuildDataService guildData)
    {
        _client = client;
        _guildData = guildData;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (_guildData.GetGuildIds().Count is 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        while (!ct.IsCancellationRequested)
        {
            var nextSong = SongList[_nextSongIndex];
            var builder = new StringBuilder().Append(nextSong.Name).Append(" / ").Append(nextSong.Author);
            _activityList[0] = new Activity(builder.ToString(), ActivityType.Listening);
            _client.SubmitCommand(
                new UpdatePresence(
                    UserStatus.Online, false, DateTimeOffset.UtcNow, _activityList));
            _nextSongIndex++;
            if (_nextSongIndex >= SongList.Length)
            {
                _nextSongIndex = 0;
            }

            await Task.Delay(nextSong.Duration, ct);
        }
    }
}
