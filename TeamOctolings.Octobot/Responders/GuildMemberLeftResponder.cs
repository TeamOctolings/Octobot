﻿using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Extensions;
using TeamOctolings.Octobot.Services;

namespace TeamOctolings.Octobot.Responders;

/// <summary>
///     Handles sending a guild's <see cref="GuildSettings.LeaveMessage" /> if one is set.
/// </summary>
/// <seealso cref="GuildSettings.LeaveMessage" />
[UsedImplicitly]
public sealed class GuildMemberLeftResponder : IResponder<IGuildMemberRemove>
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;

    public GuildMemberLeftResponder(
        IDiscordRestChannelAPI channelApi, GuildDataService guildData, IDiscordRestGuildAPI guildApi)
    {
        _channelApi = channelApi;
        _guildData = guildData;
        _guildApi = guildApi;
    }

    public async Task<Result> RespondAsync(IGuildMemberRemove gatewayEvent, CancellationToken ct = default)
    {
        var user = gatewayEvent.User;
        var data = await _guildData.GetData(gatewayEvent.GuildID, ct);
        var cfg = data.Settings;

        var memberData = data.GetOrCreateMemberData(user.ID);
        if (memberData.BannedUntil is not null || memberData.Kicked)
        {
            return Result.Success;
        }

        if (GuildSettings.WelcomeMessagesChannel.Get(cfg).Empty()
            || GuildSettings.LeaveMessage.Get(cfg) is "off" or "disable" or "disabled")
        {
            return Result.Success;
        }

        Messages.Culture = GuildSettings.Language.Get(cfg);
        var leaveMessage = GuildSettings.LeaveMessage.Get(cfg) is "default" or "reset"
            ? Messages.DefaultLeaveMessage
            : GuildSettings.LeaveMessage.Get(cfg);

        var guildResult = await _guildApi.GetGuildAsync(gatewayEvent.GuildID, ct: ct);
        if (!guildResult.IsDefined(out var guild))
        {
            return ResultExtensions.FromError(guildResult);
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(leaveMessage, user.GetTag(), guild.Name), user)
            .WithGuildFooter(guild)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColour(ColorsList.Black)
            .Build();

        return await _channelApi.CreateMessageWithEmbedResultAsync(
            GuildSettings.WelcomeMessagesChannel.Get(cfg), embedResult: embed,
            allowedMentions: Utility.NoMentions, ct: ct);
    }
}
