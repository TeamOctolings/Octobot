using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Boyfriend.Responders;

/// <summary>
///     Handles sending a notification when a scheduled event has been cancelled
///     in a guild's <see cref="GuildSettings.EventNotificationChannel" /> if one is set.
/// </summary>
[UsedImplicitly]
public class GuildScheduledEventDeleteResponder : IResponder<IGuildScheduledEventDelete> {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService       _dataService;

    public GuildScheduledEventDeleteResponder(IDiscordRestChannelAPI channelApi, GuildDataService dataService) {
        _channelApi = channelApi;
        _dataService = dataService;
    }

    public async Task<Result> RespondAsync(IGuildScheduledEventDelete gatewayEvent, CancellationToken ct = default) {
        var guildData = await _dataService.GetData(gatewayEvent.GuildID, ct);
        guildData.ScheduledEvents.Remove(gatewayEvent.ID.Value);

        if (GuildSettings.EventNotificationChannel.Get(guildData.Settings).Empty())
            return Result.FromSuccess();

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.EventCancelled, gatewayEvent.Name))
            .WithDescription(":(")
            .WithColour(ColorsList.Red)
            .WithCurrentTimestamp()
            .Build();

        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.EventNotificationChannel.Get(guildData.Settings), embeds: new[] { built }, ct: ct);
    }
}
