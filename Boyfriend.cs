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
            = (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers)
              & ~GatewayIntents.GuildInvites,
        AlwaysDownloadUsers = true,
        AlwaysResolveStickers = false,
        AlwaysDownloadDefaultStickers = false,
        LargeThreshold = 500
    };

    private static DateTimeOffset _nextSongAt = DateTimeOffset.MinValue;
    private static uint _nextSongIndex;

    private static readonly Tuple<Game, TimeSpan>[] ActivityList = {
        Tuple.Create(
            new Game("Masayoshi Minoshima (ft. nomico) - Bad Apple!!", ActivityType.Listening),
            new TimeSpan(0, 3, 40)),
        Tuple.Create(new Game("Xi - Blue Zenith", ActivityType.Listening), new TimeSpan(0, 4, 16)),
        Tuple.Create(
            new Game("UNDEAD CORPORATION - Everything will freeze", ActivityType.Listening), new TimeSpan(0, 3, 18)),
        Tuple.Create(new Game("Splatoon 3 - Candy-Coated Rocks", ActivityType.Listening), new TimeSpan(0, 2, 39)),
        Tuple.Create(new Game("RetroSpecter - Overtime", ActivityType.Listening), new TimeSpan(0, 4, 33)),
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

        await Task.Delay(-1);
    }

    private static async void TickAllGuildsAsync(object? sender, ElapsedEventArgs e) {
        if (GuildTickTasks.Count is not 0) return;

        var now = DateTimeOffset.Now;
        foreach (var guild in Client.Guilds) GuildTickTasks.Add(TickGuildAsync(guild, now));

        if (now >= _nextSongAt) {
            var nextSong = ActivityList[_nextSongIndex];
            await Client.SetActivityAsync(nextSong.Item1);
            _nextSongAt = now.Add(nextSong.Item2);
            _nextSongIndex++;
            if (_nextSongIndex >= ActivityList.Length) _nextSongIndex = 0;
        }

        try { Task.WaitAll(GuildTickTasks.ToArray()); } catch (AggregateException ex) {
            foreach (var exc in ex.InnerExceptions)
                await Log(
                    new LogMessage(
                        LogSeverity.Error, nameof(Boyfriend),
                        "Exception while ticking guilds", exc));
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

    private static async Task TickGuildAsync(SocketGuild guild, DateTimeOffset now) {
        var data = GuildData.Get(guild);
        var config = data.Preferences;
        var saveData = false;
        _ = int.TryParse(config["EventEarlyNotificationOffset"], out var offset);
        foreach (var schEvent in guild.Events)
            if (schEvent.Status is GuildScheduledEventStatus.Scheduled
                && config["AutoStartEvents"] is "true"
                && DateTimeOffset
                    .Now
                >= schEvent.StartTime) await schEvent.StartAsync();
            else if (!data.EarlyNotifications.Contains(schEvent.Id)
                     && now >= schEvent.StartTime.Subtract(new TimeSpan(0, offset, 0))) {
                data.EarlyNotifications.Add(schEvent.Id);
                var receivers = config["EventStartedReceivers"];
                var role = guild.GetRole(ulong.Parse(config["EventNotificationRole"]));
                var mentions = StringBuilder;

                if (receivers.Contains("role") && role is not null) mentions.Append($"{role.Mention} ");
                if (receivers.Contains("users") || receivers.Contains("interested"))
                    mentions = (await schEvent.GetUsersAsync(15))
                        .Where(user => role is null || !((RestGuildUser)user).RoleIds.Contains(role.Id))
                        .Aggregate(mentions, (current, user) => current.Append($"{user.Mention} "));

                await Utils.GetEventNotificationChannel(guild)?.SendMessageAsync(
                    string.Format(
                        Messages.EventEarlyNotification,
                        mentions,
                        Utils.Wrap(schEvent.Name),
                        schEvent.StartTime.ToUnixTimeSeconds().ToString()))!;
                mentions.Clear();
            }

        foreach (var mData in data.MemberData.Values) {
            var user = guild.GetUser(mData.Id);
            if (now >= mData.BannedUntil) _ = guild.RemoveBanAsync(mData.Id);
            if (!mData.IsInGuild) continue;
            if (mData.MutedUntil is null
                && ulong.TryParse(config["StarterRole"], out var starterRoleId)
                && !mData.Roles.Contains(starterRoleId)) _ = user.AddRoleAsync(starterRoleId);

            if (now >= mData.MutedUntil) {
                await Utils.UnmuteMemberAsync(
                    data, Client.CurrentUser.ToString(), user,
                    Messages.PunishmentExpired);
                saveData = true;
            }

            for (var i = mData.Reminders.Count - 1; i >= 0; i--) {
                var reminder = mData.Reminders[i];
                if (now < reminder.RemindAt) continue;

                var channel = guild.GetTextChannel(reminder.ReminderChannel);
                var toSend = $"{ReplyEmojis.Reminder} <@{mData.Id}> {Utils.Wrap(reminder.ReminderText)}";
                if (channel is not null)
                    await channel.SendMessageAsync(toSend);
                else
                    await Utils.SendDirectMessage(user, toSend);

                mData.Reminders.RemoveAt(i);
                saveData = true;
            }
        }

        if (saveData) await data.Save(true);
    }
}
