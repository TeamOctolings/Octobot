using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global

namespace Boyfriend.Commands;

public class ErrorLoggingPostExecutionEvent : IPostExecutionEvent {
    private readonly ILogger<ErrorLoggingPostExecutionEvent> _logger;

    public ErrorLoggingPostExecutionEvent(ILogger<ErrorLoggingPostExecutionEvent> logger) {
        _logger = logger;
    }

    public Task<Result> AfterExecutionAsync(
        ICommandContext context, IResult commandResult, CancellationToken ct = default) {
        if (!commandResult.IsSuccess)
            _logger.LogWarning("Error in slash command handler.\n{ErrorMessage}", commandResult.Error.Message);

        return Task.FromResult(Result.FromSuccess());
    }
}
