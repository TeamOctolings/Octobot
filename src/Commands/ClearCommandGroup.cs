using System.ComponentModel;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Extensions;
using Octobot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Commands;

/// <summary>
///     Handles the command to clear messages in a channel: /clear.
/// </summary>
[UsedImplicitly]
public class ClearCommandGroup : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public ClearCommandGroup(
        IDiscordRestChannelAPI channelApi, ICommandContext context, GuildDataService guildData,
        IFeedbackService feedback, IDiscordRestUserAPI userApi, Utility utility)
    {
        _channelApi = channelApi;
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that clears messages in the channel it was executed, optionally filtering by message author.
    /// </summary>
    /// <param name="amount">The amount of messages to clear.</param>
    /// <param name="author">The user whose messages will be cleared.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded. A successful result does not mean that any messages
    ///     were cleared and vice-versa.
    /// </returns>
    [Command("clear", "очистить")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageMessages)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [RequireBotDiscordPermissions(DiscordPermission.ManageMessages)]
    [Description("Remove multiple messages")]
    [UsedImplicitly]
    public async Task<Result> ExecuteClear(
        [Description("Number of messages to remove (2-100)")] [MinValue(2)] [MaxValue(100)]
        int amount,
        IUser? author = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        // The bot's avatar is used when sending messages
        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var messagesResult = await _channelApi.GetChannelMessagesAsync(
            channelId, limit: amount + 1, ct: CancellationToken);
        if (!messagesResult.IsDefined(out var messages))
        {
            return ResultExtensions.FromError(messagesResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ClearMessagesAsync(executor, author, data, channelId, messages, bot, CancellationToken);
    }

    private async Task<Result> ClearMessagesAsync(
        IUser executor, IUser? author, GuildData data, Snowflake channelId, IReadOnlyList<IMessage> messages, IUser bot,
        CancellationToken ct = default)
    {
        var idList = new List<Snowflake>(messages.Count);
        var builder = new StringBuilder().AppendLine(Mention.Channel(channelId)).AppendLine();
        for (var i = messages.Count - 1; i >= 1; i--) // '>= 1' to skip last message ('Octobot is thinking...')
        {
            var message = messages[i];
            if (author is not null && message.Author.ID != author.ID)
            {
                continue;
            }

            idList.Add(message.ID);
            builder.AppendLine(string.Format(Messages.MessageFrom, Mention.User(message.Author)));
            builder.Append(message.Content.InBlockCode());
        }

        if (idList.Count == 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.NoMessagesToClear, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var title = author is not null
            ? string.Format(Messages.MessagesClearedFiltered, idList.Count.ToString(), author.GetTag())
            : string.Format(Messages.MessagesCleared, idList.Count.ToString());
        var description = builder.ToString();

        var deleteResult = await _channelApi.BulkDeleteMessagesAsync(
            channelId, idList, executor.GetTag().EncodeHeader(), ct);
        if (!deleteResult.IsSuccess)
        {
            return ResultExtensions.FromError(deleteResult);
        }

        _utility.LogAction(
            data.Settings, channelId, executor, title, description, bot, ColorsList.Red, false, ct);

        var embed = new EmbedBuilder().WithSmallTitle(title, bot)
            .WithColour(ColorsList.Green).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
