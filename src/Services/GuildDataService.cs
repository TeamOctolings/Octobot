using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octobot.Data;
using Remora.Rest.Core;

namespace Octobot.Services;

/// <summary>
///     Handles saving, loading, initializing and providing <see cref="GuildData" />.
/// </summary>
public sealed class GuildDataService : BackgroundService
{
    private readonly ConcurrentDictionary<Snowflake, GuildData> _datas = new();
    private readonly ILogger<GuildDataService> _logger;

    public GuildDataService(ILogger<GuildDataService> logger)
    {
        _logger = logger;
    }

    public override Task StopAsync(CancellationToken ct)
    {
        base.StopAsync(ct);
        return SaveAsync(ct);
    }

    private Task SaveAsync(CancellationToken ct)
    {
        var tasks = new List<Task>();
        var datas = _datas.Values.ToArray();
        foreach (var data in datas.Where(data => !data.DataLoadFailed))
        {
            tasks.Add(SerializeObjectSafelyAsync(data.Settings, data.SettingsPath, ct));
            tasks.Add(SerializeObjectSafelyAsync(data.ScheduledEvents, data.ScheduledEventsPath, ct));

            var memberDatas = data.MemberData.Values.ToArray();
            tasks.AddRange(memberDatas.Select(memberData =>
                SerializeObjectSafelyAsync(memberData, $"{data.MemberDataPath}/{memberData.Id}.json", ct)));
        }

        return Task.WhenAll(tasks);
    }

    private static async Task SerializeObjectSafelyAsync<T>(T obj, string path, CancellationToken ct)
    {
        var tempFilePath = path + ".tmp";
        await using (var tempFileStream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(tempFileStream, obj, cancellationToken: ct);
        }

        File.Copy(tempFilePath, path, true);
        File.Delete(tempFilePath);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await SaveAsync(ct);
        }
    }

    public async Task<GuildData> GetData(Snowflake guildId, CancellationToken ct = default)
    {
        return _datas.TryGetValue(guildId, out var data) ? data : await InitializeData(guildId, ct);
    }

    private async Task<GuildData> InitializeData(Snowflake guildId, CancellationToken ct = default)
    {
        var path = $"GuildData/{guildId}";
        var memberDataPath = $"{path}/MemberData";
        var settingsPath = $"{path}/Settings.json";
        var scheduledEventsPath = $"{path}/ScheduledEvents.json";

        await MigrateGuildData(guildId, path, ct);

        Directory.CreateDirectory(path);

        if (!File.Exists(settingsPath))
        {
            await File.WriteAllTextAsync(settingsPath, "{}", ct);
        }

        if (!File.Exists(scheduledEventsPath))
        {
            await File.WriteAllTextAsync(scheduledEventsPath, "{}", ct);
        }

        var dataLoadFailed = false;

        await using var settingsStream = File.OpenRead(settingsPath);
        JsonNode? jsonSettings = null;
        try
        {
            jsonSettings = await JsonNode.ParseAsync(settingsStream, cancellationToken: ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Guild settings load failed: {Path}", settingsPath);
            dataLoadFailed = true;
        }

        await using var eventsStream = File.OpenRead(scheduledEventsPath);
        Dictionary<ulong, ScheduledEventData>? events = null;
        try
        {
            events = await JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                eventsStream, cancellationToken: ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Guild scheduled events load failed: {Path}", scheduledEventsPath);
            dataLoadFailed = true;
        }

        var memberData = new Dictionary<ulong, MemberData>();
        foreach (var dataFileInfo in Directory.CreateDirectory(memberDataPath).GetFiles())
        {
            await using var dataStream = dataFileInfo.OpenRead();
            MemberData? data;
            try
            {
                data = await JsonSerializer.DeserializeAsync<MemberData>(dataStream, cancellationToken: ct);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Member data load failed: {MemberDataPath}/{FileName}", memberDataPath,
                    dataFileInfo.Name);
                dataLoadFailed = true;
                continue;
            }

            if (data is null)
            {
                continue;
            }

            memberData.Add(data.Id, data);
        }

        var finalData = new GuildData(
            jsonSettings ?? new JsonObject(), settingsPath,
            events ?? new Dictionary<ulong, ScheduledEventData>(), scheduledEventsPath,
            memberData, memberDataPath,
            dataLoadFailed);

        _datas.TryAdd(guildId, finalData);

        return finalData;
    }

    private async Task MigrateGuildData(Snowflake guildId, string newPath, CancellationToken ct)
    {
        var oldPath = $"{guildId}";

        if (Directory.Exists(oldPath))
        {
            Directory.CreateDirectory($"{newPath}/..");
            Directory.Move(oldPath, newPath);

            _logger.LogInformation("Moved guild data to separate folder: \"{OldPath}\" -> \"{NewPath}\"", oldPath,
                newPath);
        }

        var settings = (await GetData(guildId, ct)).Settings;

        if (GuildSettings.Language.Get(settings).Name is "tt-RU")
        {
            GuildSettings.Language.Set(settings, "ru");
            _logger.LogInformation("Switched from unsupported language in \"{GuildID}\": mctaylors-ru -> ru", guildId.Value);
        }
    }

    public async Task<JsonNode> GetSettings(Snowflake guildId, CancellationToken ct = default)
    {
        return (await GetData(guildId, ct)).Settings;
    }

    public ICollection<Snowflake> GetGuildIds()
    {
        return _datas.Keys;
    }

    public bool UnloadGuildData(Snowflake id)
    {
        return _datas.TryRemove(id, out _);
    }
}
