using System.ComponentModel;
using System.Text;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
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

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to clear messages in a channel: /clear.
/// </summary>
[UsedImplicitly]
public class ClearCommandGroup : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestUserAPI    _userApi;
    private readonly UtilityService         _utility;

    public ClearCommandGroup(
        IDiscordRestChannelAPI channelApi,      ICommandContext     context, GuildDataService dataService,
        FeedbackService        feedbackService, IDiscordRestUserAPI userApi, UtilityService   utility) {
        _channelApi = channelApi;
        _context = context;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that clears messages in the channel it was executed.
    /// </summary>
    /// <param name="amount">The amount of messages to clear.</param>
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
        int amount) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var messagesResult = await _channelApi.GetChannelMessagesAsync(
            channelId.Value, limit: amount + 1, ct: CancellationToken);
        if (!messagesResult.IsDefined(out var messages))
            return Result.FromError(messagesResult);
        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);
        // The current user's avatar is used when sending messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var data = await _dataService.GetData(guildId.Value, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await ClearMessagesAsync(amount, data, channelId.Value, messages, user, currentUser, CancellationToken);
    }

    private async Task<Result> ClearMessagesAsync(
        int   amount, GuildData data,        Snowflake         channelId, IReadOnlyList<IMessage> messages,
        IUser user,   IUser     currentUser, CancellationToken ct = default) {
        var idList = new List<Snowflake>(messages.Count);
        var builder = new StringBuilder().AppendLine(Mention.Channel(channelId)).AppendLine();
        for (var i = messages.Count - 1; i >= 1; i--) { // '>= 1' to skip last message ('Boyfriend is thinking...')
            var message = messages[i];
            idList.Add(message.ID);
            builder.AppendLine(string.Format(Messages.MessageFrom, Mention.User(message.Author)));
            builder.Append(message.Content.InBlockCode());
        }

        var title = string.Format(Messages.MessagesCleared, amount.ToString());
        var description = builder.ToString();

        var deleteResult = await _channelApi.BulkDeleteMessagesAsync(
            channelId, idList, user.GetTag().EncodeHeader(), ct);
        if (!deleteResult.IsSuccess)
            return Result.FromError(deleteResult.Error);

        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, user, title, description, currentUser, ColorsList.Red, false, ct);
        if (!logResult.IsSuccess)
            return Result.FromError(logResult.Error);

        var embed = new EmbedBuilder().WithSmallTitle(title, currentUser)
            .WithColour(ColorsList.Green).Build();

        return await _feedbackService.SendContextualEmbedResultAsync(embed, ct);
    }
}
