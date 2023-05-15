using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Caching.Services;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Hosting.Extensions;

namespace Boyfriend;

public class Boyfriend {
    public static ILogger<Boyfriend> Logger = null!;
    public static IConfiguration GuildConfiguration = null!;

    private static readonly Dictionary<string, string> ReflectionMessageCache = new();

    public static async Task Main(string[] args) {
        var host = CreateHostBuilder(args).UseConsoleLifetime().Build();

        var services = host.Services;
        Logger = services.GetRequiredService<ILogger<Boyfriend>>();
        GuildConfiguration = services.GetRequiredService<IConfigurationBuilder>().AddJsonFile("guild_configs.json")
            .Build();

        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) {
        return Host.CreateDefaultBuilder(args)
            .AddDiscordService(
                services => {
                    var configuration = services.GetRequiredService<IConfiguration>();

                    return configuration.GetValue<string?>("BOT_TOKEN")
                           ?? throw new InvalidOperationException(
                               "No bot token has been provided. Set the "
                               + "BOT_TOKEN environment variable to a valid token.");
                }
            ).ConfigureServices(
                (_, services) => {
                    var responderTypes = typeof(Boyfriend).Assembly
                        .GetExportedTypes()
                        .Where(t => t.IsResponder());
                    foreach (var responderType in responderTypes) services.AddResponder(responderType);

                    services.AddDiscordCaching();
                    services.Configure<CacheSettings>(
                        settings => { settings.SetAbsoluteExpiration<IMessage>(TimeSpan.FromDays(7)); });

                    services.AddSingleton<IConfigurationBuilder, ConfigurationBuilder>();
                }
            ).ConfigureLogging(
                c => c.AddConsole()
                    .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                    .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
            );
    }

    public static string GetLocalized(string key) {
        var propertyName = key;
        key = $"{Messages.Culture}/{key}";
        if (ReflectionMessageCache.TryGetValue(key, out var cached)) return cached;

        var toReturn =
            typeof(Messages).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?.ToString();
        if (toReturn is null) {
            Logger.LogError("Could not find localized property: {Name}", propertyName);
            return key;
        }

        ReflectionMessageCache.Add(key, toReturn);
        return toReturn;
    }
}
