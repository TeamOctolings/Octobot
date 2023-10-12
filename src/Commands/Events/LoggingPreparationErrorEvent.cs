using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Octobot.Extensions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace Octobot.Commands.Events;

/// <summary>
///     Handles error logging for slash commands that couldn't be successfully prepared.
/// </summary>
[UsedImplicitly]
public class LoggingPreparationErrorEvent : IPreparationErrorEvent
{
    private readonly ILogger<LoggingPreparationErrorEvent> _logger;

    public LoggingPreparationErrorEvent(ILogger<LoggingPreparationErrorEvent> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Logs a warning using the injected <see cref="ILogger" /> if the <paramref name="preparationResult" /> has not
    ///     succeeded.
    /// </summary>
    /// <param name="context">The context of the slash command. Unused.</param>
    /// <param name="preparationResult">The result whose success is checked.</param>
    /// <param name="ct">The cancellation token for this operation. Unused.</param>
    /// <returns>A result which has succeeded.</returns>
    public Task<Result> PreparationFailed(
        IOperationContext context, IResult preparationResult, CancellationToken ct = default)
    {
        _logger.LogResult(preparationResult, "Error in slash command preparation.");

        return Task.FromResult(Result.FromSuccess());
    }
}
