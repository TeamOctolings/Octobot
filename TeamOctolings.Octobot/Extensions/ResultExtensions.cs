using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Remora.Results;

namespace TeamOctolings.Octobot.Extensions;

public static class ResultExtensions
{
    public static Result FromError(Result result)
    {
        LogResultStackTrace(result);

        return result;
    }

    public static Result FromError<T>(Result<T> result)
    {
        var casted = (Result)result;
        LogResultStackTrace(casted);

        return casted;
    }

    private static void LogResultStackTrace(Result result)
    {
        if (result.IsSuccess || result.Error is ExceptionError { Exception: OperationCanceledException })
        {
            return;
        }

        if (Utility.StaticLogger is null)
        {
            throw new InvalidOperationException();
        }

        Utility.StaticLogger.LogError("{ErrorType}: {ErrorMessage}{NewLine}{StackTrace}",
            result.Error.GetType().FullName, result.Error.Message, Environment.NewLine, ConstructStackTrace());

        var inner = result.Inner;
        while (inner is { IsSuccess: false })
        {
            Utility.StaticLogger.LogError("Caused by: {ResultType}: {ResultMessage}",
                inner.Error.GetType().FullName, inner.Error.Message);

            inner = inner.Inner;
        }
    }

    private static string ConstructStackTrace()
    {
        var stackArray = new StackTrace(3, true).ToString().Split(Environment.NewLine).ToList();
        for (var i = stackArray.Count - 1; i >= 0; i--)
        {
            var frame = stackArray[i];
            var trimmed = frame.TrimStart();
            if (trimmed.StartsWith("at System.Threading", StringComparison.Ordinal)
                || trimmed.StartsWith("at System.Runtime.CompilerServices", StringComparison.Ordinal))
            {
                stackArray.RemoveAt(i);
            }
        }

        return string.Join(Environment.NewLine, stackArray);
    }
}
