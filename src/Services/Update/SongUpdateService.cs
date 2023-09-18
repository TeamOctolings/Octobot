using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;

namespace Boyfriend.Services.Update;

public sealed class SongUpdateService : BackgroundService
{
    private static readonly (string Name, TimeSpan Duration)[] SongList =
    {
        ("UNDEAD CORPORATION - The Empress", new TimeSpan(0, 4, 34)),
        ("UNDEAD CORPORATION - Everything will freeze", new TimeSpan(0, 3, 17)),
        ("Splatoon 3 - Rockagilly Blues (Yoko & the Gold Bazookas)", new TimeSpan(0, 3, 37)),
        ("Splatoon 3 - Seep and Destroy", new TimeSpan(0, 2, 42)),
        ("IA - A Tale of Six Trillion Years and a Night", new TimeSpan(0, 3, 40)),
        ("Manuel - Gas Gas Gas", new TimeSpan(0, 3, 17)),
        ("Camellia - Flamewall", new TimeSpan(0, 6, 50)),
        ("Jukio Kallio, Daniel Hagström - Fall 'n' Roll", new TimeSpan(0, 3, 14)),
        ("SCATTLE - Hypertension", new TimeSpan(0, 3, 18)),
        ("KEYGEN CHURCH - Tenebre Rosso Sangue", new TimeSpan(0, 3, 53)),
        ("Chipzel - Swing Me Another 6", new TimeSpan(0, 5, 32)),
        ("Noisecream - Mist of Rage", new TimeSpan(0, 2, 25)),
        ("EDWXRDX - CONSCIENCE", new TimeSpan(0, 2, 16)),
        ("dontleaveme - afterward", new TimeSpan(0, 2, 29)),
        ("Ferdous - Gravity", new TimeSpan(0, 2, 38)),
        ("The Drums - Money", new TimeSpan(0, 3, 53)),
        ("Derek Pope - War Machine", new TimeSpan(0, 3, 39))
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
            _activityList[0] = new Activity(nextSong.Name, ActivityType.Listening);
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
