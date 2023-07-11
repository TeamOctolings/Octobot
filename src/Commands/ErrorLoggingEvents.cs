using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles error logging for slash commands that couldn't be successfully prepared.
/// </summary>
public class ErrorLoggingPreparationErrorEvent : IPreparationErrorEvent {
    private readonly ILogger<ErrorLoggingPreparationErrorEvent> _logger;

    public ErrorLoggingPreparationErrorEvent(ILogger<ErrorLoggingPreparationErrorEvent> logger) {
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
        IOperationContext context, IResult preparationResult, CancellationToken ct = default) {
        if (!preparationResult.IsSuccess)
            _logger.LogWarning("Error in slash command preparation.\n{ErrorMessage}", preparationResult.Error.Message);

        return Task.FromResult(Result.FromSuccess());
    }
}

/// <summary>
///     Handles error logging for slash command groups.
/// </summary>
public class ErrorLoggingPostExecutionEvent : IPostExecutionEvent {
    private readonly ILogger<ErrorLoggingPostExecutionEvent> _logger;

    public ErrorLoggingPostExecutionEvent(ILogger<ErrorLoggingPostExecutionEvent> logger) {
        _logger = logger;
    }

    /// <summary>
    ///     Logs a warning using the injected <see cref="ILogger" /> if the <paramref name="commandResult" /> has not
    ///     succeeded.
    /// </summary>
    /// <param name="context">The context of the slash command. Unused.</param>
    /// <param name="commandResult">The result whose success is checked.</param>
    /// <param name="ct">The cancellation token for this operation. Unused.</param>
    /// <returns>A result which has succeeded.</returns>
    public Task<Result> AfterExecutionAsync(
        ICommandContext context, IResult commandResult, CancellationToken ct = default) {
        if (!commandResult.IsSuccess)
            _logger.LogWarning("Error in slash command execution.\n{ErrorMessage}", commandResult.Error.Message);

        return Task.FromResult(Result.FromSuccess());
    }
}
