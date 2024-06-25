using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Caching.Services;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;
using Serilog.Extensions.Logging;
using TeamOctolings.Octobot.Commands.Events;
using TeamOctolings.Octobot.Services;
using TeamOctolings.Octobot.Services.Update;

namespace TeamOctolings.Octobot;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).UseConsoleLifetime().Build();
        var services = host.Services;
        Utility.StaticLogger = services.GetRequiredService<ILogger<Program>>();

        var slashService = services.GetRequiredService<SlashService>();
        // Providing a guild ID to this call will result in command duplicates!
        // To get rid of them, provide the ID of the guild containing duplicates,
        // comment out calls to WithCommandGroup in CreateHostBuilder
        // then launch the bot again and remove the guild ID
        await slashService.UpdateSlashCommandsAsync();

        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .AddDiscordService(
                services =>
                {
                    var configuration = services.GetRequiredService<IConfiguration>();

                    return configuration.GetValue<string?>("BOT_TOKEN")
                           ?? throw new InvalidOperationException(
                               "No bot token has been provided. Set the "
                               + "BOT_TOKEN environment variable to a valid token.");
                }
            ).ConfigureServices(
                (_, services) =>
                {
                    services.Configure<DiscordGatewayClientOptions>(
                        options =>
                        {
                            options.Intents |= GatewayIntents.MessageContents
                                               | GatewayIntents.GuildMembers
                                               | GatewayIntents.GuildPresences
                                               | GatewayIntents.GuildScheduledEvents;
                        });
                    services.Configure<CacheSettings>(
                        cSettings =>
                        {
                            cSettings.SetDefaultAbsoluteExpiration(TimeSpan.FromHours(1));
                            cSettings.SetDefaultSlidingExpiration(TimeSpan.FromMinutes(30));
                            cSettings.SetAbsoluteExpiration<IMessage>(TimeSpan.FromDays(7));
                            cSettings.SetSlidingExpiration<IMessage>(TimeSpan.FromDays(7));
                        });

                    services.AddTransient<IConfigurationBuilder, ConfigurationBuilder>()
                        // Init
                        .AddDiscordCaching()
                        .AddDiscordCommands(true, false)
                        .AddRespondersFromAssembly(typeof(Program).Assembly)
                        .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
                        // Slash command event handlers
                        .AddPreparationErrorEvent<LoggingPreparationErrorEvent>()
                        .AddPostExecutionEvent<ErrorLoggingPostExecutionEvent>()
                        // Services
                        .AddSingleton<AccessControlService>()
                        .AddSingleton<GuildDataService>()
                        .AddSingleton<ReminderService>()
                        .AddSingleton<Utility>()
                        .AddHostedService<GuildDataService>(provider => provider.GetRequiredService<GuildDataService>())
                        .AddHostedService<MemberUpdateService>()
                        .AddHostedService<ScheduledEventUpdateService>()
                        .AddHostedService<SongUpdateService>();
                }
            ).ConfigureLogging(
                c => c.AddConsole()
                    .AddFile("Logs/Octobot-{Date}.log",
                        outputTemplate: "{Timestamp:o} [{Level:u4}] {Message} {NewLine}{Exception}")
                    .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                    .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
                    .AddFilter<SerilogLoggerProvider>("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                    .AddFilter<SerilogLoggerProvider>("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
            );
    }
}
