using System.ComponentModel;
using System.Text;
using Boyfriend.Services;
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

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to clear messages in a channel: /clear.
/// </summary>
public class ClearCommandGroup : CommandGroup {
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext        _context;
    private readonly GuildDataService       _dataService;
    private readonly FeedbackService        _feedbackService;
    private readonly IDiscordRestUserAPI    _userApi;

    public ClearCommandGroup(
        IDiscordRestChannelAPI channelApi,      ICommandContext     context, GuildDataService dataService,
        FeedbackService        feedbackService, IDiscordRestUserAPI userApi) {
        _channelApi = channelApi;
        _context = context;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _userApi = userApi;
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
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [RequireBotDiscordPermissions(DiscordPermission.ManageMessages)]
    [Description("Remove multiple messages")]
    public async Task<Result> ClearMessagesAsync(
        [Description("Number of messages to remove (2-100)")] [MinValue(2)] [MaxValue(100)]
        int amount) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var messagesResult = await _channelApi.GetChannelMessagesAsync(
            channelId.Value, limit: amount + 1, ct: CancellationToken);
        if (!messagesResult.IsDefined(out var messages))
            return Result.FromError(messagesResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        var idList = new List<Snowflake>(messages.Count);
        var builder = new StringBuilder().AppendLine(Mention.Channel(channelId.Value)).AppendLine();
        for (var i = messages.Count - 1; i >= 1; i--) { // '>= 1' to skip last message ('Boyfriend is thinking...')
            var message = messages[i];
            idList.Add(message.ID);
            builder.AppendLine(string.Format(Messages.MessageFrom, Mention.User(message.Author)));
            builder.Append(message.Content.InBlockCode());
        }

        var description = builder.ToString();

        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        var deleteResult = await _channelApi.BulkDeleteMessagesAsync(
            channelId.Value, idList, user.GetTag().EncodeHeader(), CancellationToken);
        if (!deleteResult.IsSuccess)
            return Result.FromError(deleteResult.Error);

        // The current user's avatar is used when sending messages
        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var title = string.Format(Messages.MessagesCleared, amount.ToString());
        if (cfg.PrivateFeedbackChannel is not 0 && cfg.PrivateFeedbackChannel != channelId.Value) {
            var logEmbed = new EmbedBuilder().WithSmallTitle(title, currentUser)
                .WithDescription(description)
                .WithActionFooter(user)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Red)
                .Build();

            if (!logEmbed.IsDefined(out var logBuilt))
                return Result.FromError(logEmbed);

            // Not awaiting to reduce response time
            if (cfg.PrivateFeedbackChannel != channelId.Value)
                _ = _channelApi.CreateMessageAsync(
                    cfg.PrivateFeedbackChannel.ToDiscordSnowflake(), embeds: new[] { logBuilt },
                    ct: CancellationToken);
        }

        var embed = new EmbedBuilder().WithSmallTitle(title, currentUser)
            .WithColour(ColorsList.Green).Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
