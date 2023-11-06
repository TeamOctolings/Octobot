using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

/// <summary>
///     Handles sending a <see cref="Ready" /> message to a guild that has just initialized if that guild
///     has <see cref="GuildSettings.ReceiveStartupMessages" /> enabled
/// </summary>
[UsedImplicitly]
public class GuildLoadedResponder : IResponder<IGuildCreate>
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _guildData;
    private readonly ILogger<GuildLoadedResponder> _logger;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public GuildLoadedResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService guildData, ILogger<GuildLoadedResponder> logger,
        IDiscordRestUserAPI userApi, UtilityService utility)
    {
        _channelApi = channelApi;
        _guildData = guildData;
        _logger = logger;
        _userApi = userApi;
        _utility = utility;
    }

    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct = default)
    {
        if (!gatewayEvent.Guild.IsT0) // Guild is not IAvailableGuild
        {
            return Result.FromSuccess();
        }

        var guild = gatewayEvent.Guild.AsT0;

        var data = await _guildData.GetData(guild.ID, ct);
        var cfg = data.Settings;

        foreach (var member in guild.Members.Where(m => m.User.HasValue))
        {
            data.GetOrCreateMemberData(member.User.Value.ID);
        }

        var botResult = await _userApi.GetCurrentUserAsync(ct);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        if (data.DataLoadFailed)
        {
            return await SendDataLoadFailed(guild, data, bot, ct);
        }

        var ownerResult = await _userApi.GetUserAsync(guild.OwnerID, ct);
        if (!ownerResult.IsDefined(out var owner))
        {
            return Result.FromError(ownerResult);
        }

        _logger.LogInformation("Loaded guild \"{Name}\" ({ID}) owned by {Owner} ({OwnerID}) with {MemberCount} members",
            guild.Name, guild.ID, owner.GetTag(), owner.ID, guild.MemberCount);

        if (!GuildSettings.ReceiveStartupMessages.Get(cfg))
        {
            return Result.FromSuccess();
        }

        if (GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty())
        {
            return Result.FromSuccess();
        }

        Messages.Culture = GuildSettings.Language.Get(cfg);
        var i = Random.Shared.Next(1, 4);

        var embed = new EmbedBuilder().WithSmallTitle(bot.GetTag(), bot)
            .WithTitle($"Sound{i}".Localized())
            .WithDescription(Messages.Ready)
            .WithCurrentTimestamp()
            .WithColour(ColorsList.Blue)
            .Build();
        if (!embed.IsDefined(out var built))
        {
            return Result.FromError(embed);
        }

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.PrivateFeedbackChannel.Get(cfg), embeds: new[] { built }, ct: ct);
    }

    private async Task<Result> SendDataLoadFailed(IGuild guild, GuildData data, IUser bot, CancellationToken ct)
    {
        var errorEmbed = new EmbedBuilder()
            .WithSmallTitle(Messages.DataLoadFailedTitle, bot)
            .WithDescription(Messages.DataLoadFailedDescription)
            .WithFooter(Messages.ContactDevelopers)
            .WithColour(ColorsList.Red)
            .Build();

        if (!errorEmbed.IsDefined(out var errorBuilt))
        {
            return Result.FromError(errorEmbed);
        }

        var channelResult = await _utility.GetEmergencyFeedbackChannel(guild, data, ct);
        if (!channelResult.IsDefined(out var channel))
        {
            return Result.FromError(channelResult);
        }

        return (Result)await _channelApi.CreateMessageAsync(
            channel, embeds: new[] { errorBuilt }, ct: ct);
    }
}
