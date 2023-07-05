using System.ComponentModel;
using Boyfriend.Services.Data;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to get the time taken for the gateway to respond to the last heartbeat: /ping
/// </summary>
public class PingCommandGroup : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly DiscordGatewayClient   _client;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestUserAPI    _userApi;

    public PingCommandGroup(
        IDiscordRestChannelAPI channelApi,  ICommandContext context,         DiscordGatewayClient client,
        GuildDataService       dataService, FeedbackService feedbackService, IDiscordRestUserAPI  userApi) {
        _channelApi = channelApi;
        _context = context;
        _client = client;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that shows time taken for the gateway to respond to the last heartbeat.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("ping", "пинг")]
    [Description("получает задержку")]
    public async Task<Result> GetPingAsync() {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        var latency = _client.Latency.TotalMilliseconds;
        if (latency is 0) {
            // No heartbeat has occurred, estimate latency from local time and "Boyfriend is thinking..." message
            var lastMessageResult = await _channelApi.GetChannelMessagesAsync(
                channelId.Value, limit: 1, ct: CancellationToken);
            if (!lastMessageResult.IsDefined(out var lastMessage))
                return Result.FromError(lastMessageResult);
            latency = DateTimeOffset.UtcNow.Subtract(lastMessage.Single().Timestamp).TotalMilliseconds;
        }

        var embed = new EmbedBuilder().WithSmallTitle(currentUser.GetTag(), currentUser)
            .WithTitle($"Beep{Random.Shared.Next(1, 4)}".Localized())
            .WithDescription($"{latency:F0}{Messages.Milliseconds}")
            .WithColour(latency < 250 ? ColorsList.Green : latency < 500 ? ColorsList.Yellow : ColorsList.Red)
            .WithCurrentTimestamp()
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
