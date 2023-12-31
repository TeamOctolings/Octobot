using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Remora.Commands.Parsers;
using Remora.Results;

namespace Octobot.Parsers;

/// <summary>
///     Parses <see cref="TimeSpan"/>s.
/// </summary>
[PublicAPI]
public partial class TimeSpanParser : AbstractTypeParser<TimeSpan>
{
    private static readonly Regex Pattern = ParseRegex();

    /// <summary>
    ///     Parses <see cref="TimeSpan"/> from <see cref="string"/>.
    /// </summary>
    /// <returns>
    ///     Parsed <see cref="TimeSpan"/>.
    /// </returns>
    /// <remarks>
    ///     If parse wasn't successful, <see cref="TimeSpanParser"/> will return <see cref="ArgumentInvalidError"/>.
    /// </remarks>
    public static Result<TimeSpan> TryParse(string timeSpanString)
    {
        if (timeSpanString.StartsWith('-'))
        {
            return new ArgumentInvalidError(nameof(timeSpanString), "TimeSpans cannot be negative.");
        }

        if (TimeSpan.TryParse(timeSpanString, DateTimeFormatInfo.InvariantInfo, out var parsedTimeSpan))
        {
            return parsedTimeSpan;
        }

        var matches = ParseRegex().Matches(timeSpanString);
        if (matches.Count is 0)
        {
            return new ArgumentInvalidError(nameof(timeSpanString), "The regex did not produce any matches.");
        }

        return ParseFromRegex(matches);
    }

    private static TimeSpan ParseFromRegex(MatchCollection matches)
    {
        var timeSpan = TimeSpan.Zero;

        foreach (var groups in matches.Select(match => match.Groups
                     .Cast<Group>()
                     .Where(g => g.Success)
                     .Skip(1)
                     .Select(g => (g.Name, g.Value))))
        {
            foreach ((var key, var groupValue) in groups)
            {
                if (!double.TryParse(groupValue, out var parsedGroupValue) ||
                    !int.TryParse(groupValue, out var parsedIntegerValue))
                {
                    return TimeSpan.Zero;
                }

                var now = DateTimeOffset.UtcNow;
                timeSpan += key switch
                {
                    "Years" => now.AddYears(parsedIntegerValue) - now,
                    "Months" => now.AddMonths(parsedIntegerValue) - now,
                    "Weeks" => TimeSpan.FromDays(parsedGroupValue * 7),
                    "Days" => TimeSpan.FromDays(parsedGroupValue),
                    "Hours" => TimeSpan.FromHours(parsedGroupValue),
                    "Minutes" => TimeSpan.FromMinutes(parsedGroupValue),
                    "Seconds" => TimeSpan.FromSeconds(parsedGroupValue),
                    _ => throw new ArgumentOutOfRangeException(key)
                };
            }
        }

        return timeSpan;
    }

    [GeneratedRegex("(?<Years>\\d+(?=y|л|г))|(?<Months>\\d+(?=mo|мес))|(?<Weeks>\\d+(?=w|н|нед))|(?<Days>\\d+(?=d|дн))|(?<Hours>\\d+(?=h|ч))|(?<Minutes>\\d+(?=m|min|мин|м))|(?<Seconds>\\d+(?=s|sec|с|сек))")]
    private static partial Regex ParseRegex();
}
