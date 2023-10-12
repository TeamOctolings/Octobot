using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace Octobot.Extensions;

public static class FeedbackServiceExtensions
{
    public static async Task<Result> SendContextualEmbedResultAsync(
        this FeedbackService feedback, Result<Embed> embedResult, CancellationToken ct = default)
    {
        if (!embedResult.IsDefined(out var embed))
        {
            return Result.FromError(embedResult);
        }

        return (Result)await feedback.SendContextualEmbedAsync(embed, ct: ct);
    }
}
