using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace Octobot.Responders;

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
            return Result.Success;
        }

        var cfg = await _guildData.GetSettings(guildId, ct);
        if (GuildSettings.PrivateFeedbackChannel.Get(cfg).Empty())
        {
            return Result.Success;
        }

        var messageResult = await _channelApi.GetChannelMessageAsync(gatewayEvent.ChannelID, gatewayEvent.ID, ct);
        if (!messageResult.IsDefined(out var message))
        {
            return ResultExtensions.FromError(messageResult);
        }

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return Result.Success;
        }

        var auditLogResult = await _auditLogApi.GetGuildAuditLogAsync(
            guildId, actionType: AuditLogEvent.MessageDelete, limit: 1, ct: ct);
        if (!auditLogResult.IsDefined(out var auditLogPage))
        {
            return ResultExtensions.FromError(auditLogResult);
        }

        var auditLog = auditLogPage.AuditLogEntries.Single();

        var deleterResult = Result<IUser>.FromSuccess(message.Author);
        if (auditLog.UserID is not null
            && auditLog.Options.Value.ChannelID == gatewayEvent.ChannelID
            && DateTimeOffset.UtcNow.Subtract(auditLog.ID.Timestamp).TotalSeconds <= 2)
        {
            deleterResult = await _userApi.GetUserAsync(auditLog.UserID.Value, ct);
        }

        if (!deleterResult.IsDefined(out var deleter))
        {
            return ResultExtensions.FromError(deleterResult);
        }

        Messages.Culture = GuildSettings.Language.Get(cfg);

        var builder = new StringBuilder()
            .AppendLine(message.Content.InBlockCode())
            .AppendLine(
                string.Format(Messages.DescriptionActionJumpToChannel, Mention.Channel(gatewayEvent.ChannelID))
            );

        var embed = new EmbedBuilder()
            .WithSmallTitle(
                string.Format(
                    Messages.CachedMessageDeleted,
                    message.Author.GetTag()), message.Author)
            .WithDescription(builder.ToString())
            .WithActionFooter(deleter)
            .WithTimestamp(message.Timestamp)
            .WithColour(ColorsList.Red)
            .Build();

        return await _channelApi.CreateMessageWithEmbedResultAsync(
            GuildSettings.PrivateFeedbackChannel.Get(cfg), embedResult: embed,
            allowedMentions: Octobot.NoMentions, ct: ct);
    }
}
