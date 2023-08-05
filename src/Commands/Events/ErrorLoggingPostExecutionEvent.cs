using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace Boyfriend.Commands.Events;

/// <summary>
///     Handles error logging for slash command groups.
/// </summary>
[UsedImplicitly]
public class ErrorLoggingPostExecutionEvent : IPostExecutionEvent
{
    private readonly ILogger<ErrorLoggingPostExecutionEvent> _logger;

    public ErrorLoggingPostExecutionEvent(ILogger<ErrorLoggingPostExecutionEvent> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Logs a warning using the injected <see cref="ILogger" /> if the <paramref name="commandResult" /> has not
    ///     succeeded.
    /// </summary>
    /// <param name="context">The context of the slash command.</param>
    /// <param name="commandResult">The result whose success is checked.</param>
    /// <param name="ct">The cancellation token for this operation. Unused.</param>
    /// <returns>A result which has succeeded.</returns>
    public Task<Result> AfterExecutionAsync(
        ICommandContext context, IResult commandResult, CancellationToken ct = default)
    {
        _logger.LogResult(commandResult, $"Error in slash command execution for /{context.Command.Command.Node.Key}.");

        return Task.FromResult(Result.FromSuccess());
    }
}
