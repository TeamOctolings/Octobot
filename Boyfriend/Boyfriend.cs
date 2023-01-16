using System.Text;
using System.Timers;
using Boyfriend.Data;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Timer = System.Timers.Timer;

namespace Boyfriend;

public static class Boyfriend {
    public static readonly StringBuilder StringBuilder = new();

    private static readonly DiscordSocketConfig Config = new() {
        MessageCacheSize = 250,
        GatewayIntents
            = (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers) &
              ~GatewayIntents.GuildInvites,
        AlwaysDownloadUsers = true,
        AlwaysResolveStickers = false,
        AlwaysDownloadDefaultStickers = false,
        LargeThreshold = 500
    };

    private static DateTimeOffset _nextSongAt = DateTimeOffset.MinValue;
    private static uint _nextSongIndex;

    private static readonly Tuple<Game, TimeSpan>[] ActivityList = {
        Tuple.Create(new Game("Masayoshi Minoshima (ft. nomico) - Bad Apple!!", ActivityType.Listening),
            new TimeSpan(0, 3, 40)),
        Tuple.Create(new Game("Xi - Blue Zenith", ActivityType.Listening), new TimeSpan(0, 4, 16)),
        Tuple.Create(new Game("Kurokotei - Scattered Faith", ActivityType.Listening), new TimeSpan(0, 8, 21)),
        Tuple.Create(new Game("Splatoon 3 - Candy-Coated Rocks", ActivityType.Listening), new TimeSpan(0, 2, 39)),
        Tuple.Create(new Game("RetroSpecter - Genocide", ActivityType.Listening), new TimeSpan(0, 5, 52)),
        Tuple.Create(new Game("beatMARIO - Night of Knights", ActivityType.Listening), new TimeSpan(0, 4, 10))
    };

    public static readonly DiscordSocketClient Client = new(Config);

    private static readonly List<Task> GuildTickTasks = new();

    public static void Main() {
        InitAsync().GetAwaiter().GetResult();
    }

    private static async Task InitAsync() {
        var token = (await File.ReadAllTextAsync("token.txt")).Trim();

        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        EventHandler.InitEvents();

        var timer = new Timer();
        timer.Interval = 1000;
        timer.AutoReset = true;
        timer.Elapsed += TickAllGuildsAsync;
        if (ActivityList.Length is 0) timer.Dispose(); // CodeQL moment
        timer.Start();

        while (ActivityList.Length > 0)
            if (DateTimeOffset.Now >= _nextSongAt) {
                var nextSong = ActivityList[_nextSongIndex];
                await Client.SetActivityAsync(nextSong.Item1);
                _nextSongAt = DateTimeOffset.Now.Add(nextSong.Item2);
                _nextSongIndex++;
                if (_nextSongIndex >= ActivityList.Length) _nextSongIndex = 0;
            }
    }

    private static async void TickAllGuildsAsync(object? sender, ElapsedEventArgs e) {
        foreach (var guild in Client.Guilds) GuildTickTasks.Add(TickGuildAsync(guild));

        try { Task.WaitAll(GuildTickTasks.ToArray()); } catch (AggregateException ex) {
            foreach (var exc in ex.InnerExceptions)
                await Log(new LogMessage(LogSeverity.Error, nameof(CommandProcessor),
                    "Exception while executing commands", exc));
        }

        GuildTickTasks.Clear();
    }

    public static Task Log(LogMessage msg) {
        switch (msg.Severity) {
            case LogSeverity.Critical:
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine(msg.ToString());
                break;
            case LogSeverity.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(msg.ToString());
                break;
            case LogSeverity.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(msg.ToString());
                break;
            case LogSeverity.Info:
                Console.WriteLine(msg.ToString());
                break;

            case LogSeverity.Verbose:
            case LogSeverity.Debug:
            default: return Task.CompletedTask;
        }

        Console.ResetColor();
        return Task.CompletedTask;
    }

    private static async Task TickGuildAsync(SocketGuild guild) {
        var data = GuildData.Get(guild);
        var config = data.Preferences;
        _ = int.TryParse(config["EventEarlyNotificationOffset"], out var offset);
        foreach (var schEvent in guild.Events)
            if (config["AutoStartEvents"] is "true" && DateTimeOffset.Now >= schEvent.StartTime) {
                await schEvent.StartAsync();
            } else if (!data.EarlyNotifications.Contains(schEvent.Id) &&
                       DateTimeOffset.Now >= schEvent.StartTime.Subtract(new TimeSpan(0, offset, 0))) {
                var receivers = config["EventStartedReceivers"];
                var role = guild.GetRole(ulong.Parse(config["EventNotificationRole"]));
                var mentions = StringBuilder;

                if (receivers.Contains("role") && role is not null) mentions.Append($"{role.Mention} ");
                if (receivers.Contains("users") || receivers.Contains("interested"))
                    mentions = (await schEvent.GetUsersAsync(15))
                        .Where(user => role is null || !((RestGuildUser)user).RoleIds.Contains(role.Id))
                        .Aggregate(mentions, (current, user) => current.Append($"{user.Mention} "));

                await Utils.GetEventNotificationChannel(guild)?.SendMessageAsync(string.Format(Messages.EventStarted,
                    mentions,
                    Utils.Wrap(schEvent.Name),
                    Utils.Wrap(schEvent.Location) ?? Utils.MentionChannel(schEvent.Channel.Id)))!;
                mentions.Clear();
                data.EarlyNotifications.Add(schEvent.Id);
            }

        foreach (var mData in data.MemberData.Values) {
            if (DateTimeOffset.Now >= mData.BannedUntil) _ = guild.RemoveBanAsync(mData.Id);

            if (mData.IsInGuild) {
                if (DateTimeOffset.Now >= mData.MutedUntil)
                    await Utils.UnmuteMemberAsync(data, Client.CurrentUser.ToString(), guild.GetUser(mData.Id),
                        Messages.PunishmentExpired);

                foreach (var reminder in mData.Reminders.Where(rem => DateTimeOffset.Now >= rem.RemindAt)) {
                    var channel = guild.GetTextChannel(reminder.ReminderChannel);
                    if (channel is null) {
                        await Utils.SendDirectMessage(Client.GetUser(mData.Id), reminder.ReminderText);
                        continue;
                    }

                    await channel.SendMessageAsync($"<@{mData.Id}> {Utils.Wrap(reminder.ReminderText)}");

                    mData.Reminders.Remove(reminder);
                }
            }
        }
    }
}
