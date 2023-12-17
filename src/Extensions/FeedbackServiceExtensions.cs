using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace Octobot.Extensions;

public static class FeedbackServiceExtensions
{
    public static async Task<Result> SendContextualEmbedResultAsync(
        this IFeedbackService feedback, Result<Embed> embedResult, CancellationToken ct = default,
        FeedbackMessageOptions? options = null)
    {
        if (!embedResult.IsDefined(out var embed))
        {
            return Result.FromError(embedResult);
        }

        return (Result)await feedback.SendContextualEmbedAsync(embed, options, ct);
    }
}
