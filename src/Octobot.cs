using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octobot.Commands.Events;
using Octobot.Services;
using Octobot.Services.Update;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Caching.Services;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;
using Remora.Rest.Core;
using Serilog.Extensions.Logging;

namespace Octobot;

public sealed class Octobot
{
    public const string RepositoryUrl = "https://github.com/LabsDevelopment/Octobot";
    public const string IssuesUrl = $"{RepositoryUrl}/issues";

    public static readonly AllowedMentions NoMentions = new(
        Array.Empty<MentionType>(), Array.Empty<Snowflake>(), Array.Empty<Snowflake>());

    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).UseConsoleLifetime().Build();
        var services = host.Services;

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
                        .AddRespondersFromAssembly(typeof(Octobot).Assembly)
                        .AddCommandGroupsFromAssembly(typeof(Octobot).Assembly)
                        // Slash command event handlers
                        .AddPreparationErrorEvent<LoggingPreparationErrorEvent>()
                        .AddPostExecutionEvent<ErrorLoggingPostExecutionEvent>()
                        // Services
                        .AddSingleton<Utility>()
                        .AddSingleton<GuildDataService>()
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
