using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Boyfriend.Responders;

/// <summary>
///     Handles sending a <see cref="Ready" /> message to a guild that has just initialized if that guild
///     has <see cref="GuildSettings.ReceiveStartupMessages" /> enabled
/// </summary>
[UsedImplicitly]
public class GuildLoadedResponder : IResponder<IGuildCreate> {
    private readonly IDiscordRestChannelAPI        _channelApi;
    private readonly GuildDataService              _dataService;
    private readonly ILogger<GuildLoadedResponder> _logger;
    private readonly IDiscordRestUserAPI           _userApi;

    public GuildLoadedResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService dataService, ILogger<GuildLoadedResponder> logger,
        IDiscordRestUserAPI    userApi) {
        _channelApi = channelApi;
        _dataService = dataService;
        _logger = logger;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default) {
        if (!gatewayEvent.Guild.IsT0) return Result.FromSuccess(); // Guild is not IAvailableGuild

        var guild = gatewayEvent.Guild.AsT0;
        _logger.LogInformation("Joined guild \"{Name}\"", guild.Name);

        var cfg = await _dataService.GetSettings(guild.ID, ct);
        if (!GuildSettings.ReceiveStartupMessages.Get(cfg))
            return Result.FromSuccess();
        if (GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty())
            return Result.FromSuccess();

        var currentUserResult = await _userApi.GetCurrentUserAsync(ct);
        if (!currentUserResult.IsDefined(out var currentUser)) return Result.FromError(currentUserResult);

        Messages.Culture = GuildSettings.Language.Get(cfg);
        var i = Random.Shared.Next(1, 4);

        var embed = new EmbedBuilder().WithSmallTitle(currentUser.GetTag(), currentUser)
            .WithTitle($"Beep{i}".Localized())
            .WithDescription(Messages.Ready)
            .WithCurrentTimestamp()
            .WithColour(ColorsList.Blue)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.PrivateFeedbackChannel.Get(cfg), embeds: new[] { built }, ct: ct);
    }
}
