using System.Text;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Boyfriend.Responders;

/// <summary>
///     Handles logging the contents of a deleted message and the user who deleted the message
///     to a guild's <see cref="GuildSettings.PrivateFeedbackChannel" /> if one is set.
/// </summary>
[UsedImplicitly]
public class MessageDeletedResponder : IResponder<IMessageDelete>
{
    private readonly IDiscordRestAuditLogAPI _auditLogApi;
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public MessageDeletedResponder(
        IDiscordRestAuditLogAPI auditLogApi, IDiscordRestChannelAPI channelApi,
        GuildDataService guildData, IDiscordRestUserAPI userApi)
    {
        _auditLogApi = auditLogApi;
        _channelApi = channelApi;
        _guildData = guildData;
        _userApi = userApi;
    }

    public async Task<Result> RespondAsync(IMessageDelete gatewayEvent, CancellationToken ct = default)
    {
        if (!gatewayEvent.GuildID.IsDefined(out var guildId))
        {
            return Result.FromSuccess();
        }

        var cfg = await _guildData.GetSettings(guildId, ct);
        if (GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty())
        {
            return Result.FromSuccess();
        }

        var messageResult = await _channelApi.GetChannelMessageAsync(gatewayEvent.ChannelID, gatewayEvent.ID, ct);
        if (!messageResult.IsDefined(out var message))
        {
            return Result.FromError(messageResult);
        }

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return Result.FromSuccess();
        }

        var auditLogResult = await _auditLogApi.GetGuildAuditLogAsync(
            guildId, actionType: AuditLogEvent.MessageDelete, limit: 1, ct: ct);
        if (!auditLogResult.IsDefined(out var auditLogPage))
        {
            return Result.FromError(auditLogResult);
        }

        var auditLog = auditLogPage.AuditLogEntries.Single();

        var userResult = Result<IUser>.FromSuccess(message.Author);
        if (auditLog.UserID is not null
            && auditLog.Options.Value.ChannelID == gatewayEvent.ChannelID
            && DateTimeOffset.UtcNow.Subtract(auditLog.ID.Timestamp).TotalSeconds <= 2)
        {
            userResult = await _userApi.GetUserAsync(auditLog.UserID.Value, ct);
        }

        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        Messages.Culture = GuildSettings.Language.Get(cfg);

        var builder = new StringBuilder().AppendLine(
                string.Format(Messages.DescriptionActionJumpToChannel,
                    Mention.Channel(gatewayEvent.ChannelID)))
            .AppendLine(message.Content.InBlockCode());

        var embed = new EmbedBuilder()
            .WithSmallTitle(
                string.Format(
                    Messages.CachedMessageDeleted,
                    message.Author.GetTag()), message.Author)
            .WithDescription(builder.ToString())
            .WithActionFooter(user)
            .WithTimestamp(message.Timestamp)
            .WithColour(ColorsList.Red)
            .Build();
        if (!embed.IsDefined(out var built))
        {
            return Result.FromError(embed);
        }

        return (Result)await _channelApi.CreateMessageAsync(
            GuildSettings.PrivateFeedbackChannel.Get(cfg), embeds: new[] { built },
            allowedMentions: Boyfriend.NoMentions, ct: ct);
    }
}
