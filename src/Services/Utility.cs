using System.Drawing;
using System.Text;
using System.Text.Json.Nodes;
using Octobot.Data;
using Octobot.Extensions;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Services;

/// <summary>
///     Provides utility methods that cannot be transformed to extension methods because they require usage
///     of some Discord APIs.
/// </summary>
public sealed class Utility
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestGuildScheduledEventAPI _eventApi;
    private readonly IDiscordRestGuildAPI _guildApi;

    public Utility(
        IDiscordRestChannelAPI channelApi, IDiscordRestGuildScheduledEventAPI eventApi, IDiscordRestGuildAPI guildApi)
    {
        _channelApi = channelApi;
        _eventApi = eventApi;
        _guildApi = guildApi;
    }

    /// <summary>
    ///     Gets the string mentioning the <see cref="GuildSettings.EventNotificationRole" /> and event subscribers related to
    ///     a scheduled
    ///     event.
    /// </summary>
    /// <param name="scheduledEvent">
    ///     The scheduled event whose subscribers will be mentioned.
    /// </param>
    /// <param name="data">The data of the guild containing the scheduled event.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A result containing the string which may or may not have succeeded.</returns>
    public async Task<Result<string>> GetEventNotificationMentions(
        IGuildScheduledEvent scheduledEvent, GuildData data, CancellationToken ct = default)
    {
        var builder = new StringBuilder();
        var role = GuildSettings.EventNotificationRole.Get(data.Settings);
        var subscribersResult = await _eventApi.GetGuildScheduledEventUsersAsync(
            scheduledEvent.GuildID, scheduledEvent.ID, ct: ct);
        if (!subscribersResult.IsDefined(out var subscribers))
        {
            return Result<string>.FromError(subscribersResult);
        }

        if (!role.Empty())
        {
            builder.Append($"{Mention.Role(role)} ");
        }

        builder = subscribers.Where(
                subscriber => !data.GetOrCreateMemberData(subscriber.User.ID).Roles.Contains(role.Value))
            .Aggregate(builder, (current, subscriber) => current.Append($"{Mention.User(subscriber.User)} "));
        return builder.ToString();
    }

    /// <summary>
    ///     Logs an action in the <see cref="GuildSettings.PublicFeedbackChannel" /> and
    ///     <see cref="GuildSettings.PrivateFeedbackChannel" />.
    /// </summary>
    /// <param name="cfg">The guild configuration.</param>
    /// <param name="channelId">The ID of the channel where the action was executed.</param>
    /// <param name="user">The user who performed the action.</param>
    /// <param name="title">The title for the embed.</param>
    /// <param name="description">The description of the embed.</param>
    /// <param name="avatar">The user whose avatar will be displayed next to the <paramref name="title" /> of the embed.</param>
    /// <param name="color">The color of the embed.</param>
    /// <param name="isPublic">
    ///     Whether or not the embed should be sent in <see cref="GuildSettings.PublicFeedbackChannel" />
    /// </param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A result which has succeeded.</returns>
    public void LogAction(
        JsonNode cfg, Snowflake channelId, IUser user, string title, string description, IUser avatar,
        Color color, bool isPublic = true, CancellationToken ct = default)
    {
        var publicChannel = GuildSettings.PublicFeedbackChannel.Get(cfg);
        var privateChannel = GuildSettings.PrivateFeedbackChannel.Get(cfg);
        if (GuildSettings.PublicFeedbackChannel.Get(cfg).EmptyOrEqualTo(channelId)
            && GuildSettings.PrivateFeedbackChannel.Get(cfg).EmptyOrEqualTo(channelId))
        {
            return;
        }

        var logEmbed = new EmbedBuilder().WithSmallTitle(title, avatar)
            .WithDescription(description)
            .WithActionFooter(user)
            .WithCurrentTimestamp()
            .WithColour(color)
            .Build();

        // Not awaiting to reduce response time
        if (isPublic && publicChannel != channelId)
        {
            _ = _channelApi.CreateMessageWithEmbedResultAsync(
                publicChannel, embedResult: logEmbed,
                ct: ct);
        }

        if (privateChannel != publicChannel
            && privateChannel != channelId)
        {
            _ = _channelApi.CreateMessageWithEmbedResultAsync(
                privateChannel, embedResult: logEmbed,
                ct: ct);
        }
    }

    public async Task<Result<Snowflake>> GetEmergencyFeedbackChannel(IGuild guild, GuildData data, CancellationToken ct)
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

        if (guild.SystemChannelID.AsOptional().IsDefined(out var systemChannel))
        {
            return systemChannel;
        }

        var channelsResult = await _guildApi.GetGuildChannelsAsync(guild.ID, ct);

        return channelsResult.IsDefined(out var channels)
            ? channels[0].ID
            : Result<Snowflake>.FromError(channelsResult);
    }
}
