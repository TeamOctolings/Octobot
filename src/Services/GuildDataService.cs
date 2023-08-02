using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Boyfriend.Data;
using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;

namespace Boyfriend.Services;

/// <summary>
///     Handles saving, loading, initializing and providing <see cref="GuildData" />.
/// </summary>
public sealed class GuildDataService : IHostedService
{
    private readonly ConcurrentDictionary<Snowflake, GuildData> _datas = new();
    private readonly IDiscordRestGuildAPI _guildApi;

    // https://github.com/dotnet/aspnetcore/issues/39139
    public GuildDataService(
        IHostApplicationLifetime lifetime, IDiscordRestGuildAPI guildApi)
    {
        _guildApi = guildApi;
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

    private async Task SaveAsync(CancellationToken ct)
    {
        var tasks = new List<Task>();
        foreach (var data in _datas.Values)
        {
            await using var settingsStream = File.OpenWrite(data.SettingsPath);
            tasks.Add(JsonSerializer.SerializeAsync(settingsStream, data.Settings, cancellationToken: ct));

            await using var eventsStream = File.OpenWrite(data.ScheduledEventsPath);
            tasks.Add(JsonSerializer.SerializeAsync(eventsStream, data.ScheduledEvents, cancellationToken: ct));

            foreach (var memberData in data.MemberData.Values)
            {
                await using var memberDataStream = File.OpenWrite($"{data.MemberDataPath}/{memberData.Id}.json");
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
        var idString = $"{guildId}";
        var memberDataPath = $"{guildId}/MemberData";
        var settingsPath = $"{guildId}/Settings.json";
        var scheduledEventsPath = $"{guildId}/ScheduledEvents.json";
        if (!Directory.Exists(idString))
        {
            Directory.CreateDirectory(idString);
        }

        if (!Directory.Exists(memberDataPath))
        {
            Directory.CreateDirectory(memberDataPath);
        }

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
            = JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                eventsStream, cancellationToken: ct);

        var memberData = new Dictionary<ulong, MemberData>();
        foreach (var dataPath in Directory.GetFiles(memberDataPath))
        {
            await using var dataStream = File.OpenRead(dataPath);
            var data = await JsonSerializer.DeserializeAsync<MemberData>(dataStream, cancellationToken: ct);
            if (data is null)
            {
                continue;
            }

            var memberResult = await _guildApi.GetGuildMemberAsync(guildId, data.Id.ToSnowflake(), ct);
            if (memberResult.IsSuccess)
            {
                data.Roles = memberResult.Entity.Roles.ToList().ConvertAll(r => r.Value);
            }

            memberData.Add(data.Id, data);
        }

        var finalData = new GuildData(
            jsonSettings ?? new JsonObject(), settingsPath,
            await events ?? new Dictionary<ulong, ScheduledEventData>(), scheduledEventsPath,
            memberData, memberDataPath);
        while (!_datas.ContainsKey(guildId))
        {
            _datas.TryAdd(guildId, finalData);
        }

        return finalData;
    }

    public async Task<JsonNode> GetSettings(Snowflake guildId, CancellationToken ct = default)
    {
        return (await GetData(guildId, ct)).Settings;
    }

    public async Task<MemberData> GetMemberData(Snowflake guildId, Snowflake userId, CancellationToken ct = default)
    {
        return (await GetData(guildId, ct)).GetMemberData(userId);
    }

    public ICollection<Snowflake> GetGuildIds()
    {
        return _datas.Keys;
    }
}
