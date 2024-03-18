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
    private readonly IDiscordRestUserAPI _userApi;

    public Utility(
        IDiscordRestChannelAPI channelApi, IDiscordRestGuildScheduledEventAPI eventApi, IDiscordRestGuildAPI guildApi,
        IDiscordRestUserAPI userApi)
    {
        _channelApi = channelApi;
        _eventApi = eventApi;
        _guildApi = guildApi;
        _userApi = userApi;
    }

    /// <summary>
    ///     Checks whether or not a member can interact with another member
    /// </summary>
    /// <param name="guildId">The ID of the guild in which an operation is being performed.</param>
    /// <param name="interacterId">The executor of the operation.</param>
    /// <param name="targetId">The target of the operation.</param>
    /// <param name="action">The operation.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>
    ///     <list type="bullet">
    ///         <item>A result which has succeeded with a null string if the member can interact with the target.</item>
    ///         <item>
    ///             A result which has succeeded with a non-null string containing the error message if the member cannot
    ///             interact with the target.
    ///         </item>
    ///         <item>A result which has failed if an error occurred during the execution of this method.</item>
    ///     </list>
    /// </returns>
    public async Task<Result<string?>> CheckInteractionsAsync(
        Snowflake guildId, Snowflake? interacterId, Snowflake targetId, string action, CancellationToken ct = default)
    {
        if (interacterId == targetId)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Themselves".Localized());
        }

        var botResult = await _userApi.GetCurrentUserAsync(ct);
        if (!botResult.IsDefined(out var bot))
        {
            return Result<string?>.FromError(botResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: ct);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result<string?>.FromError(guildResult);
        }

        var targetMemberResult = await _guildApi.GetGuildMemberAsync(guildId, targetId, ct);
        if (!targetMemberResult.IsDefined(out var targetMember))
        {
            return Result<string?>.FromSuccess(null);
        }

        var currentMemberResult = await _guildApi.GetGuildMemberAsync(guildId, bot.ID, ct);
        if (!currentMemberResult.IsDefined(out var currentMember))
        {
            return Result<string?>.FromError(currentMemberResult);
        }

        var rolesResult = await _guildApi.GetGuildRolesAsync(guildId, ct);
        if (!rolesResult.IsDefined(out var roles))
        {
            return Result<string?>.FromError(rolesResult);
        }

        if (interacterId is null)
        {
            return CheckInteractions(action, guild, roles, targetMember, currentMember, currentMember);
        }

        var interacterResult = await _guildApi.GetGuildMemberAsync(guildId, interacterId.Value, ct);
        return interacterResult.IsDefined(out var interacter)
            ? CheckInteractions(action, guild, roles, targetMember, currentMember, interacter)
            : Result<string?>.FromError(interacterResult);
    }

    private static Result<string?> CheckInteractions(
        string action, IGuild guild, IReadOnlyList<IRole> roles, IGuildMember targetMember, IGuildMember currentMember,
        IGuildMember interacter)
    {
        if (!targetMember.User.IsDefined(out var targetUser))
        {
            return new ArgumentNullError(nameof(targetMember.User));
        }

        if (!interacter.User.IsDefined(out var interacterUser))
        {
            return new ArgumentNullError(nameof(interacter.User));
        }

        if (currentMember.User == targetMember.User)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Bot".Localized());
        }

        if (targetUser.ID == guild.OwnerID)
        {
            return Result<string?>.FromSuccess($"UserCannot{action}Owner".Localized());
        }

        var targetRoles = roles.Where(r => targetMember.Roles.Contains(r.ID)).ToList();
        var botRoles = roles.Where(r => currentMember.Roles.Contains(r.ID));

        var targetBotRoleDiff = targetRoles.MaxOrDefault(r => r.Position) - botRoles.MaxOrDefault(r => r.Position);
        if (targetBotRoleDiff >= 0)
        {
            return Result<string?>.FromSuccess($"BotCannot{action}Target".Localized());
        }

        if (interacterUser.ID == guild.OwnerID)
        {
            return Result<string?>.FromSuccess(null);
        }

        var interacterRoles = roles.Where(r => interacter.Roles.Contains(r.ID));
        var targetInteracterRoleDiff
            = targetRoles.MaxOrDefault(r => r.Position) - interacterRoles.MaxOrDefault(r => r.Position);
        return targetInteracterRoleDiff < 0
            ? Result<string?>.FromSuccess(null)
            : Result<string?>.FromSuccess($"UserCannot{action}Target".Localized());
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
