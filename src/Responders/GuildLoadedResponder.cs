using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
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

    public GuildLoadedResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService guildData, ILogger<GuildLoadedResponder> logger,
        IDiscordRestUserAPI userApi)
    {
        _channelApi = channelApi;
        _guildData = guildData;
        _logger = logger;
        _userApi = userApi;
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

            return (Result)await _channelApi.CreateMessageAsync(
                GetEmergencyFeedbackChannel(guild, data), embeds: new[] { errorBuilt }, ct: ct);
        }

        _logger.LogInformation("Loaded guild {ID} (\"{Name}\")", guild.ID, guild.Name);

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

    private static Snowflake GetEmergencyFeedbackChannel(IGuildCreate.IAvailableGuild guild, GuildData data)
    {
        var privateFeedback = GuildSettings.PrivateFeedbackChannel.Get(data.Settings);
        if (!privateFeedback.Empty())
        {
            return privateFeedback;
        }

        var publicFeedback = GuildSettings.PublicFeedbackChannel.Get(data.Settings);
        if (!publicFeedback.Empty())
        {
            return publicFeedback;
        }

        return guild.SystemChannelID.AsOptional().IsDefined(out var systemChannel)
            ? systemChannel
            : guild.Channels[0].ID;
    }
}
