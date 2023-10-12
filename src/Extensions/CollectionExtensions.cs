using Remora.Results;

namespace Octobot.Extensions;

public static class CollectionExtensions
{
    public static TResult? MaxOrDefault<TSource, TResult>(
        this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        var list = source.ToList();
        return list.Any() ? list.Max(selector) : default;
    }

    public static void AddIfFailed(this List<Result> list, Result result)
    {
        if (!result.IsSuccess)
        {
            list.Add(result);
        }
    }

    /// <summary>
    ///     Return an appropriate result for a list of failed results. The list must only contain failed results.
    /// </summary>
    /// <param name="list">The list of failed results.</param>
    /// <returns>
    ///     A successful result if the list is empty, the only Result in the list, or <see cref="AggregateError" />
    ///     containing all results from the list.
    /// </returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Result AggregateErrors(this List<Result> list)
    {
        return list.Count switch
        {
            0 => Result.FromSuccess(),
            1 => list[0],
            _ => new AggregateError(list.Cast<IResult>().ToArray())
        };
    }
}
