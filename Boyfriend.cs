using Boyfriend.Commands;
using Boyfriend.Services;
using Boyfriend.Services.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Commands.Extensions;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Caching.Services;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Rest.Core;

namespace Boyfriend;

public class Boyfriend {
    public static readonly AllowedMentions NoMentions = new(
        Array.Empty<MentionType>(), Array.Empty<Snowflake>(), Array.Empty<Snowflake>());

    public static async Task Main(string[] args) {
        var host = CreateHostBuilder(args).UseConsoleLifetime().Build();
        var services = host.Services;

        var slashService = services.GetRequiredService<SlashService>();
        await slashService.UpdateSlashCommandsAsync();

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
                    services.Configure<DiscordGatewayClientOptions>(
                        options => {
                            options.Intents |= GatewayIntents.MessageContents
                                               | GatewayIntents.GuildMembers
                                               | GatewayIntents.GuildScheduledEvents;
                            options.Presence = new UpdatePresence(
                                UserStatus.Online, false, DateTimeOffset.UtcNow,
                                new[] { new Activity("with Remora.Discord", ActivityType.Game) });
                        });
                    services.Configure<CacheSettings>(
                        settings => {
                            settings.SetDefaultAbsoluteExpiration(TimeSpan.FromHours(1));
                            settings.SetDefaultSlidingExpiration(TimeSpan.FromMinutes(30));
                            settings.SetAbsoluteExpiration<IMessage>(TimeSpan.FromDays(7));
                            settings.SetSlidingExpiration<IMessage>(TimeSpan.FromDays(7));
                        });

                    services.AddTransient<IConfigurationBuilder, ConfigurationBuilder>()
                        .AddDiscordCaching()
                        .AddDiscordCommands(true)
                        .AddPostExecutionEvent<ErrorLoggingPostExecutionEvent>()
                        .AddInteractivity()
                        .AddInteractionGroup<InteractionResponders>()
                        .AddSingleton<GuildDataService>()
                        .AddSingleton<UtilityService>()
                        .AddCommandTree()
                        .WithCommandGroup<BanCommandGroup>();
                    var responderTypes = typeof(Boyfriend).Assembly
                        .GetExportedTypes()
                        .Where(t => t.IsResponder());
                    foreach (var responderType in responderTypes) services.AddResponder(responderType);
                }
            ).ConfigureLogging(
                c => c.AddConsole()
                    .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                    .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
            );
    }
}
