using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Extensions;
using Remora.Results;

namespace Octobot.Extensions;

public static class LoggerExtensions
{
    /// <summary>
    ///     Checks if the <paramref name="result" /> has failed due to an error that has resulted from neither invalid user
    ///     input nor the execution environment and logs the error using the provided <paramref name="logger" />.
    /// </summary>
    /// <remarks>
    ///     This has special behavior for <see cref="ExceptionError" /> - its exception will be passed to the
    ///     <paramref name="logger" />
    /// </remarks>
    /// <param name="logger">The logger to use.</param>
    /// <param name="result">The Result whose error check.</param>
    /// <param name="message">The message to use if this result has failed.</param>
    public static void LogResult(this ILogger logger, IResult result, string? message = "")
    {
        if (result.IsSuccess || result.Error.IsUserOrEnvironmentError())
        {
            return;
        }

        if (result.Error is ExceptionError exe)
        {
            logger.LogError(exe.Exception, "{ErrorMessage}", message);
            return;
        }

        logger.LogWarning("{UserMessage}\n{ResultErrorMessage}", message, result.Error.Message);
    }
}
