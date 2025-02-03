using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Rest.Core;
using TeamOctolings.Octobot.Data;

namespace TeamOctolings.Octobot.Services;

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

    private Task SaveAsync(CancellationToken ct = default)
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

    private static async Task SerializeObjectSafelyAsync<T>(T obj, string path, CancellationToken ct = default)
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

        MigrateDataDirectory(guildId, path);

        Directory.CreateDirectory(path);

        var dataLoadFailed = false;

        var jsonSettings = await LoadGuildSettings(settingsPath, ct);
        if (jsonSettings is not null)
        {
            FixJsonSettings(jsonSettings);
        }
        else
        {
            dataLoadFailed = true;
        }

        var events = await LoadScheduledEvents(scheduledEventsPath, ct);
        if (events is null)
        {
            dataLoadFailed = true;
        }

        var memberData = new Dictionary<ulong, MemberData>();
        foreach (var dataFileInfo in Directory.CreateDirectory(memberDataPath).GetFiles()
                     .Where(dataFileInfo =>
                         !memberData.ContainsKey(
                             ulong.Parse(dataFileInfo.Name.Replace(".json", "").Replace(".tmp", "")))))
        {
            var data = await LoadMemberData(dataFileInfo, memberDataPath, true, ct);

            if (data == null)
            {
                dataLoadFailed = true;
                continue;
            }

            memberData.TryAdd(data.Id, data);
        }

        var finalData = new GuildData(
            jsonSettings ?? new JsonObject(), settingsPath,
            events ?? new Dictionary<ulong, ScheduledEventData>(), scheduledEventsPath,
            memberData, memberDataPath,
            dataLoadFailed);

        _datas.TryAdd(guildId, finalData);

        return finalData;
    }

    private async Task<MemberData?> LoadMemberData(FileInfo dataFileInfo, string memberDataPath, bool loadTmp,
        CancellationToken ct = default)
    {
        MemberData? data;
        var temporaryPath = $"{dataFileInfo.FullName}.tmp";
        var usedInfo = loadTmp && File.Exists(temporaryPath) ? new FileInfo(temporaryPath) : dataFileInfo;

        var isTmp = usedInfo.Extension is ".tmp";
        try
        {
            await using var dataStream = usedInfo.OpenRead();
            data = await JsonSerializer.DeserializeAsync<MemberData>(dataStream, cancellationToken: ct);
            if (isTmp)
            {
                usedInfo.CopyTo(usedInfo.FullName.Replace(".tmp", ""), true);
                usedInfo.Delete();
            }
        }
        catch (Exception e)
        {
            if (isTmp)
            {
                _logger.LogWarning(e,
                    "Unable to load temporary member data file, deleting: {MemberDataPath}/{FileName}", memberDataPath,
                    usedInfo.Name);
                usedInfo.Delete();
                return await LoadMemberData(dataFileInfo, memberDataPath, false, ct);
            }

            _logger.LogError(e, "Member data load failed: {MemberDataPath}/{FileName}", memberDataPath,
                usedInfo.Name);
            return null;
        }

        return data;
    }

    private async Task<Dictionary<ulong, ScheduledEventData>?> LoadScheduledEvents(string scheduledEventsPath,
        CancellationToken ct = default)
    {
        var tempScheduledEventsPath = $"{scheduledEventsPath}.tmp";

        if (!File.Exists(scheduledEventsPath) && !File.Exists(tempScheduledEventsPath))
        {
            return new Dictionary<ulong, ScheduledEventData>();
        }

        if (File.Exists(tempScheduledEventsPath))
        {
            _logger.LogWarning("Found temporary scheduled events file, will try to parse and copy to main: ${Path}",
                tempScheduledEventsPath);
            try
            {
                await using var tempEventsStream = File.OpenRead(tempScheduledEventsPath);
                var events = await JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                    tempEventsStream, cancellationToken: ct);
                File.Copy(tempScheduledEventsPath, scheduledEventsPath, true);
                File.Delete(tempScheduledEventsPath);

                _logger.LogInformation("Successfully loaded temporary scheduled events file: ${Path}",
                    tempScheduledEventsPath);
                return events;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to load temporary scheduled events file: {Path}, deleting",
                    tempScheduledEventsPath);
                File.Delete(tempScheduledEventsPath);
            }
        }

        try
        {
            await using var eventsStream = File.OpenRead(scheduledEventsPath);
            return await JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                eventsStream, cancellationToken: ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Guild scheduled events load failed: {Path}", scheduledEventsPath);
            return null;
        }
    }

    private async Task<JsonNode?> LoadGuildSettings(string settingsPath, CancellationToken ct = default)
    {
        var tempSettingsPath = $"{settingsPath}.tmp";

        if (!File.Exists(settingsPath) && !File.Exists(tempSettingsPath))
        {
            return new JsonObject();
        }

        if (File.Exists(tempSettingsPath))
        {
            _logger.LogWarning("Found temporary settings file, will try to parse and copy to main: ${Path}",
                tempSettingsPath);
            try
            {
                await using var tempSettingsStream = File.OpenRead(tempSettingsPath);
                var jsonSettings = await JsonNode.ParseAsync(tempSettingsStream, cancellationToken: ct);

                File.Copy(tempSettingsPath, settingsPath, true);
                File.Delete(tempSettingsPath);

                _logger.LogInformation("Successfully loaded temporary settings file: ${Path}", tempSettingsPath);
                return jsonSettings;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to load temporary settings file: {Path}, deleting", tempSettingsPath);
                File.Delete(tempSettingsPath);
            }
        }

        try
        {
            await using var settingsStream = File.OpenRead(settingsPath);
            return await JsonNode.ParseAsync(settingsStream, cancellationToken: ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Guild settings load failed: {Path}", settingsPath);
            return null;
        }
    }

    private void MigrateDataDirectory(Snowflake guildId, string newPath)
    {
        var oldPath = $"{guildId}";

        if (Directory.Exists(oldPath))
        {
            Directory.CreateDirectory($"{newPath}/..");
            Directory.Move(oldPath, newPath);

            _logger.LogInformation("Moved guild data to separate folder: \"{OldPath}\" -> \"{NewPath}\"", oldPath,
                newPath);
        }
    }

    private static void FixJsonSettings(JsonNode settings)
    {
        var language = settings[GuildSettings.Language.Name]?.GetValue<string>();
        if (language is "mctaylors-ru")
        {
            settings[GuildSettings.Language.Name] = "ru";
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
