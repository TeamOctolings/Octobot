using System.ComponentModel;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Octobot.Services.Profiler;
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
    private readonly Profiler _profiler;
    private readonly IDiscordRestUserAPI _userApi;

    public PingCommandGroup(
        IDiscordRestChannelAPI channelApi, ICommandContext context, DiscordGatewayClient client,
        GuildDataService guildData, IFeedbackService feedback, IDiscordRestUserAPI userApi, Profiler profiler)
    {
        _channelApi = channelApi;
        _context = context;
        _client = client;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
        _profiler = profiler;
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
        _profiler.Push("ping_command");
        _profiler.Push("preparation");
        if (true || !_context.TryGetContextIDs(out var guildId, out var channelId, out _))
        {
            return _profiler.ReportWithResult(new ArgumentInvalidError(nameof(_context),
                "Unable to retrieve necessary IDs from command context"));
        }

        _profiler.Push("current_user_get");
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return _profiler.ReportWithResult(Result.FromError(botResult));
        }

        _profiler.Pop();
        _profiler.Push("guild_settings_get");
        var cfg = await _guildData.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);
        _profiler.Pop();

        _profiler.Pop();
        return _profiler.ReportWithResult(await SendLatencyAsync(channelId, bot, CancellationToken));
    }

    private async Task<Result> SendLatencyAsync(
        Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        _profiler.Push("main");
        var latency = _client.Latency.TotalMilliseconds;
        if (latency is 0)
        {
            _profiler.Push("channel_messages_get");
            // No heartbeat has occurred, estimate latency from local time and "Octobot is thinking..." message
            var lastMessageResult = await _channelApi.GetChannelMessagesAsync(
                channelId, limit: 1, ct: ct);
            if (!lastMessageResult.IsDefined(out var lastMessage))
            {
                return Result.FromError(lastMessageResult);
            }

            _profiler.Pop();
            latency = DateTimeOffset.UtcNow.Subtract(lastMessage.Single().Timestamp).TotalMilliseconds;
        }

        _profiler.Push("embed_send");
        var embed = new EmbedBuilder().WithSmallTitle(bot.GetTag(), bot)
            .WithTitle($"Sound{Random.Shared.Next(1, 4)}".Localized())
            .WithDescription($"{latency:F0}{Messages.Milliseconds}")
            .WithColour(latency < 250 ? ColorsList.Green : latency < 500 ? ColorsList.Yellow : ColorsList.Red)
            .WithCurrentTimestamp()
            .Build();

        return _profiler.PopWithResult(await _feedback.SendContextualEmbedResultAsync(embed, ct: ct));
    }
}
