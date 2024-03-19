using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Octobot.Extensions;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

namespace Octobot.Commands.Events;

/// <summary>
///     Handles error logging for slash command groups.
/// </summary>
[UsedImplicitly]
public class ErrorLoggingPostExecutionEvent : IPostExecutionEvent
{
    private readonly ILogger<ErrorLoggingPostExecutionEvent> _logger;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestUserAPI _userApi;

    public ErrorLoggingPostExecutionEvent(ILogger<ErrorLoggingPostExecutionEvent> logger, IFeedbackService feedback,
        IDiscordRestUserAPI userApi)
    {
        _logger = logger;
        _feedback = feedback;
        _userApi = userApi;
    }

    /// <summary>
    ///     Logs a warning using the injected <see cref="ILogger" /> if the <paramref name="commandResult" /> has not
    ///     succeeded.
    /// </summary>
    /// <param name="context">The context of the slash command.</param>
    /// <param name="commandResult">The result whose success is checked.</param>
    /// <param name="ct">The cancellation token for this operation. Unused.</param>
    /// <returns>A result which has succeeded.</returns>
    public async Task<Result> AfterExecutionAsync(
        ICommandContext context, IResult commandResult, CancellationToken ct = default)
    {
        _logger.LogResult(commandResult, $"Error in slash command execution for /{context.Command.Command.Node.Key}.");

        var result = commandResult;
        while (result.Inner is not null)
        {
            result = result.Inner;
        }

        if (result.IsSuccess)
        {
            return Result.FromSuccess();
        }

        var botResult = await _userApi.GetCurrentUserAsync(ct);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.CommandExecutionFailed, bot)
            .WithDescription(Markdown.InlineCode(result.Error.Message))
            .WithFooter(Messages.ContactDevelopers)
            .WithColour(ColorsList.Red)
            .Build();

        if (BuildInfo.IsDirty)
        {
            var dirtyButton = new ButtonComponent(
                ButtonComponentStyle.Link,
                Messages.ButtonDirty,
                new PartialEmoji(Name: "⚠️"),
                URL: BuildInfo.IssuesUrl,
                IsDisabled: true
            );

            return await _feedback.SendContextualEmbedResultAsync(embed,
                new FeedbackMessageOptions(MessageComponents: new[]
                {
                    new ActionRowComponent(new[] { dirtyButton })
                }), ct);
        }

        var issuesButton = new ButtonComponent(
            ButtonComponentStyle.Link,
            Messages.ButtonReportIssue,
            new PartialEmoji(Name: "⚠️"),
            URL: BuildInfo.IssuesUrl
        );

        return await _feedback.SendContextualEmbedResultAsync(embed,
            new FeedbackMessageOptions(MessageComponents: new[]
            {
                new ActionRowComponent(new[] { issuesButton })
            }), ct);
    }
}
