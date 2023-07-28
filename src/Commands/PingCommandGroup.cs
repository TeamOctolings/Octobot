using System.ComponentModel;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to get the time taken for the gateway to respond to the last heartbeat: /ping
/// </summary>
[UsedImplicitly]
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
    [Description("Get bot latency")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [UsedImplicitly]
    public async Task<Result> ExecutePingAsync() {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out _))
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        return await SendLatencyAsync(channelId, currentUser, CancellationToken);
    }

    private async Task<Result> SendLatencyAsync(
        Snowflake channelId, IUser currentUser, CancellationToken ct = default) {
        var latency = _client.Latency.TotalMilliseconds;
        if (latency is 0) {
            // No heartbeat has occurred, estimate latency from local time and "Boyfriend is thinking..." message
            var lastMessageResult = await _channelApi.GetChannelMessagesAsync(
                channelId, limit: 1, ct: ct);
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

        return await _feedbackService.SendContextualEmbedResultAsync(embed, ct);
    }
}
