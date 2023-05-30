using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Remora.Rest.Core;

namespace Boyfriend.Data.Services;

public class GuildDataService : IHostedService {
    private readonly Dictionary<Snowflake, GuildData> _datas = new();

    public Task StartAsync(CancellationToken ct) {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct) {
        var tasks = new List<Task>();
        foreach (var data in _datas.Values) {
            await using var stream = File.OpenWrite(data.ConfigurationPath);
            tasks.Add(JsonSerializer.SerializeAsync(stream, data.Configuration, cancellationToken: ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task<GuildData> GetData(Snowflake guildId, CancellationToken ct = default) {
        return _datas.TryGetValue(guildId, out var data) ? data : await InitializeData(guildId, ct);
    }

    private async Task<GuildData> InitializeData(Snowflake guildId, CancellationToken ct = default) {
        var idString = $"{guildId}";
        var memberDataDir = $"{guildId}/MemberData";
        var configurationPath = $"{guildId}/Configuration.json";
        if (!Directory.Exists(idString)) Directory.CreateDirectory(idString);
        if (!Directory.Exists(memberDataDir)) Directory.CreateDirectory(memberDataDir);
        if (!File.Exists(configurationPath)) await File.WriteAllTextAsync(configurationPath, "{}", ct);

        await using var stream = File.OpenRead(configurationPath);
        var configuration
            = JsonSerializer.DeserializeAsync<GuildConfiguration>(
                stream, cancellationToken: ct);

        var data = new GuildData(await configuration ?? new GuildConfiguration(), configurationPath);
        _datas.Add(guildId, data);
        return data;
    }

    public async Task<GuildConfiguration> GetConfiguration(Snowflake guildId, CancellationToken ct = default) {
        return (await GetData(guildId, ct)).Configuration;
    }
}
