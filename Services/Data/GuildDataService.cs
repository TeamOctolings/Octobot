using System.Text.Json;
using Boyfriend.Data;
using Microsoft.Extensions.Hosting;
using Remora.Rest.Core;

namespace Boyfriend.Services.Data;

public class GuildDataService : IHostedService {
    private readonly Dictionary<Snowflake, GuildData> _datas = new();

    public Task StartAsync(CancellationToken ct) {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct) {
        var tasks = new List<Task>();
        foreach (var data in _datas.Values) {
            await using var configStream = File.OpenWrite(data.ConfigurationPath);
            tasks.Add(JsonSerializer.SerializeAsync(configStream, data.Configuration, cancellationToken: ct));

            await using var eventsStream = File.OpenWrite(data.ScheduledEventsPath);
            tasks.Add(JsonSerializer.SerializeAsync(eventsStream, data.ScheduledEvents, cancellationToken: ct));
        }

        await Task.WhenAll(tasks);
    }

    public async Task<GuildData> GetData(Snowflake guildId, CancellationToken ct = default) {
        return _datas.TryGetValue(guildId, out var data) ? data : await InitializeData(guildId, ct);
    }

    private async Task<GuildData> InitializeData(Snowflake guildId, CancellationToken ct = default) {
        var idString = $"{guildId}";
        var memberDataDir = $"{guildId}/MemberData";
        var configurationPath = $"{guildId}/Configuration.json";
        var scheduledEventsPath = $"{guildId}/ScheduledEvents.json";
        if (!Directory.Exists(idString)) Directory.CreateDirectory(idString);
        if (!Directory.Exists(memberDataDir)) Directory.CreateDirectory(memberDataDir);
        if (!File.Exists(configurationPath)) await File.WriteAllTextAsync(configurationPath, "{}", ct);
        if (!File.Exists(scheduledEventsPath)) await File.WriteAllTextAsync(scheduledEventsPath, "{}", ct);

        await using var configurationStream = File.OpenRead(configurationPath);
        var configuration
            = JsonSerializer.DeserializeAsync<GuildConfiguration>(
                configurationStream, cancellationToken: ct);

        await using var eventsStream = File.OpenRead(scheduledEventsPath);
        var events
            = JsonSerializer.DeserializeAsync<Dictionary<ulong, ScheduledEventData>>(
                eventsStream, cancellationToken: ct);

        var data = new GuildData(
            await configuration ?? new GuildConfiguration(), configurationPath,
            await events ?? new Dictionary<ulong, ScheduledEventData>(),
            scheduledEventsPath);
        _datas.Add(guildId, data);
        return data;
    }

    public async Task<GuildConfiguration> GetConfiguration(Snowflake guildId, CancellationToken ct = default) {
        return (await GetData(guildId, ct)).Configuration;
    }
}
