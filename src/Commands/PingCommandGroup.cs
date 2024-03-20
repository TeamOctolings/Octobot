using System.ComponentModel;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
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

namespace Octobot.Commands;

/// <summary>
///     Handles the command to get the time taken for the gateway to respond to the last heartbeat: /ping
/// </summary>
[UsedImplicitly]
public class PingCommandGroup : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly DiscordGatewayClient _client;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public PingCommandGroup(
        IDiscordRestChannelAPI channelApi, ICommandContext context, DiscordGatewayClient client,
        GuildDataService guildData, IFeedbackService feedback, IDiscordRestUserAPI userApi)
    {
        _channelApi = channelApi;
        _context = context;
        _client = client;
        _guildData = guildData;
        _feedback = feedback;
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
    public async Task<Result> ExecutePingAsync()
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out _))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        var cfg = await _guildData.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        return await SendLatencyAsync(channelId, bot, CancellationToken);
    }

    private async Task<Result> SendLatencyAsync(
        Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var latency = _client.Latency.TotalMilliseconds;
        if (latency is 0)
        {
            // No heartbeat has occurred, estimate latency from local time and "Octobot is thinking..." message
            var lastMessageResult = await _channelApi.GetChannelMessagesAsync(
                channelId, limit: 1, ct: ct);
            if (!lastMessageResult.IsDefined(out var lastMessage))
            {
                return Result.FromError(lastMessageResult);
            }

            latency = DateTimeOffset.UtcNow.Subtract(lastMessage.Single().Timestamp).TotalMilliseconds;
        }

        var embed = new EmbedBuilder().WithSmallTitle(bot.GetTag(), bot)
            .WithTitle($"Generic{Random.Shared.Next(1, 4)}".Localized())
            .WithDescription($"{latency:F0}{Messages.Milliseconds}")
            .WithColour(latency < 250 ? ColorsList.Green : latency < 500 ? ColorsList.Yellow : ColorsList.Red)
            .WithCurrentTimestamp()
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
