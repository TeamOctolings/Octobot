using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octobot.Data;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;

namespace Octobot.Services;

/// <summary>
///     Handles saving, loading, initializing and providing <see cref="GuildData" />.
/// </summary>
public sealed class GuildDataService : IHostedService
{
    private readonly ConcurrentDictionary<Snowflake, GuildData> _datas = new();
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly ILogger<GuildDataService> _logger;

    // https://github.com/dotnet/aspnetcore/issues/39139
    public GuildDataService(
        IHostApplicationLifetime lifetime, IDiscordRestGuildAPI guildApi, ILogger<GuildDataService> logger)
    {
        _guildApi = guildApi;
        _logger = logger;
        lifetime.ApplicationStopping.Register(ApplicationStopping);
    }

    public Task StartAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private void ApplicationStopping()
    {
        SaveAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        var tasks = new List<Task>();
        var datas = _datas.Values.ToArray();
        foreach (var data in datas)
        {
            await using var settingsStream = File.Create(data.SettingsPath);
            tasks.Add(JsonSerializer.SerializeAsync(settingsStream, data.Settings, cancellationToken: ct));

            await using var eventsStream = File.Create(data.ScheduledEventsPath);
            tasks.Add(JsonSerializer.SerializeAsync(eventsStream, data.ScheduledEvents, cancellationToken: ct));

            var memberDatas = data.MemberData.Values.ToArray();
            foreach (var memberData in memberDatas)
            {
                await using var memberDataStream = File.Create($"{data.MemberDataPath}/{memberData.Id}.json");
                tasks.Add(JsonSerializer.SerializeAsync(memberDataStream, memberData, cancellationToken: ct));
            }
        }

        await Task.WhenAll(tasks);
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

        MigrateGuildData(guildId, path);

        Directory.CreateDirectory(path);

        if (!File.Exists(settingsPath))
        {
            await File.WriteAllTextAsync(settingsPath, "{}", ct);
        }

        if (!File.Exists(scheduledEventsPath))
        {
            await File.WriteAllTextAsync(scheduledEventsPath, "{}", ct);
        }

        await using var settingsStream = File.OpenRead(settingsPath);
        var jsonSettings
            = JsonNode.Parse(settingsStream);

        await using var eventsStream = File.OpenRead(scheduledEventsPath);
        var events
            = await JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                eventsStream, cancellationToken: ct);

        var memberData = new Dictionary<ulong, MemberData>();
        foreach (var dataFileInfo in Directory.CreateDirectory(memberDataPath).GetFiles())
        {
            await using var dataStream = dataFileInfo.OpenRead();
            var data = await JsonSerializer.DeserializeAsync<MemberData>(dataStream, cancellationToken: ct);
            if (data is null)
            {
                continue;
            }

            memberData.Add(data.Id, data);
        }

        var finalData = new GuildData(
            jsonSettings ?? new JsonObject(), settingsPath,
            events ?? new Dictionary<ulong, ScheduledEventData>(), scheduledEventsPath,
            memberData, memberDataPath);

        _datas.TryAdd(guildId, finalData);

        return finalData;
    }

    private void MigrateGuildData(Snowflake guildId, string newPath)
    {
        var oldPath = $"{guildId}";

        if (Directory.Exists(oldPath))
        {
            Directory.CreateDirectory($"{newPath}/..");
            Directory.Move(oldPath, newPath);

            _logger.LogInformation("Moved guild data to separate folder: \"{OldPath}\" -> \"{NewPath}\"", oldPath, newPath);
        }
    }

    public async Task<JsonNode> GetSettings(Snowflake guildId, CancellationToken ct = default)
    {
        return (await GetData(guildId, ct)).Settings;
    }

    public async Task<MemberData> GetMemberData(Snowflake guildId, Snowflake userId, CancellationToken ct = default)
    {
        return (await GetData(guildId, ct)).GetOrCreateMemberData(userId);
    }

    public ICollection<Snowflake> GetGuildIds()
    {
        return _datas.Keys;
    }
}
