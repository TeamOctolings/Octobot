using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;

namespace Octobot.Services.Update;

public sealed class SongUpdateService : BackgroundService
{
    private static readonly (string Author, string Name, TimeSpan Duration)[] SongList =
    [
        ("Yoko & the Gold Bazookas", "Rockagilly Blues", new TimeSpan(0, 2, 52)),
        ("Deep Cut", "Big Betrayal", new TimeSpan(0, 5, 55)),
        ("Squid Sisters", "Tomorrow's Nostalgia Today", new TimeSpan(0, 3, 7)),
        ("Deep Cut", "Anarchy Rainbow", new TimeSpan(0, 3, 20)),
        ("Squid Sisters feat. Ian BGM", "Liquid Sunshine", new TimeSpan(0, 2, 37)),
        ("Damp Socks feat. Off the Hook", "Candy-Coated Rocks", new TimeSpan(0, 2, 58)),
        ("H2Whoa", "Aquasonic", new TimeSpan(0, 2, 51)),
        ("Yoko & the Gold Bazookas", "Ska-BLAM", new TimeSpan(0, 2, 57)),
        ("Off the Hook", "Muck Warfare", new TimeSpan(0, 3, 20)),
        ("Off the Hook", "Acid Hues", new TimeSpan(0, 3, 15)),
        ("Off the Hook", "Shark Bytes", new TimeSpan(0, 3, 34)),
        ("Squid Sisters", "Calamari Inkantation", new TimeSpan(0, 2, 14)),
        ("Squid Sisters", "Ink Me Up", new TimeSpan(0, 2, 13)),
        ("Chirpy Chips", "No Quarters", new TimeSpan(0, 2, 36)),
        ("Chirpy Chips", "Shellfie", new TimeSpan(0, 2, 1)),
        ("Dedf1sh", "#11 above", new TimeSpan(0, 2, 10)),
        ("Callie", "Bomb Rush Blush", new TimeSpan(0, 2, 18)),
        ("Turquoise October", "Octoling Rendezvous", new TimeSpan(0, 1, 57)),
        ("Damp Socks feat. Off the Hook", "Tentacle to the Metal", new TimeSpan(0, 2, 51)),
        ("Off the Hook", "Fly Octo Fly ~ Ebb & Flow (Octo)", new TimeSpan(0, 3, 5))
    ];

    private static readonly (string Author, string Name, TimeSpan Duration)[] SpecialSongList =
    [
        ("Squid Sisters", "Maritime Memory", new TimeSpan(0, 2, 47))
    ];

    private readonly List<Activity> _activityList = [new Activity("with Remora.Discord", ActivityType.Game)];

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
            var nextSong = NextSong();
            _activityList[0] = new Activity($"{nextSong.Name} / {nextSong.Author}",
                ActivityType.Listening);
            _client.SubmitCommand(
                new UpdatePresence(
                    UserStatus.Online, false, DateTimeOffset.UtcNow, _activityList));

            await Task.Delay(nextSong.Duration, ct);
        }
    }

    private (string Author, string Name, TimeSpan Duration) NextSong()
    {
        var today = DateTime.Today;
        // Discontinuation of Online Services for Nintendo Wii U
        if (today.Day is 8 or 9 && today.Month is 4)
        {
            return SpecialSongList[0]; // Maritime Memory / Squid Sisters
        }

        var nextSong = SongList[_nextSongIndex];
        _nextSongIndex++;
        if (_nextSongIndex >= SongList.Length)
        {
            _nextSongIndex = 0;
        }

        return nextSong;
    }
}
