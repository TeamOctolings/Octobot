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
    ///     Parses a <see cref="TimeSpan"/> from the <paramref name="timeSpanString"/>.
    /// </summary>
    /// <returns>
    ///     The parsed <see cref="TimeSpan"/>, or <see cref="ArgumentInvalidError"/> if parsing failed.
    /// </returns>
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
        return matches.Count > 0
            ? ParseFromRegex(matches)
            : new ArgumentInvalidError(nameof(timeSpanString), "The regex did not produce any matches.");
    }

    private static Result<TimeSpan> ParseFromRegex(MatchCollection matches)
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
                if (!int.TryParse(groupValue, out var parsedIntegerValue))
                {
                    return new ArgumentInvalidError(nameof(groupValue), "The input value was not an integer.");
                }

                var now = DateTimeOffset.UtcNow;
                timeSpan += key switch
                {
                    "Years" => now.AddYears(parsedIntegerValue) - now,
                    "Months" => now.AddMonths(parsedIntegerValue) - now,
                    "Weeks" => TimeSpan.FromDays(parsedIntegerValue * 7),
                    "Days" => TimeSpan.FromDays(parsedIntegerValue),
                    "Hours" => TimeSpan.FromHours(parsedIntegerValue),
                    "Minutes" => TimeSpan.FromMinutes(parsedIntegerValue),
                    "Seconds" => TimeSpan.FromSeconds(parsedIntegerValue),
                    _ => throw new ArgumentOutOfRangeException(key)
                };
            }
        }

        return timeSpan;
    }

    [GeneratedRegex("(?<Years>\\d+(?=y|л|г))|(?<Months>\\d+(?=mo|мес))|(?<Weeks>\\d+(?=w|н|нед))|(?<Days>\\d+(?=d|дн))|(?<Hours>\\d+(?=h|ч))|(?<Minutes>\\d+(?=m|min|мин|м))|(?<Seconds>\\d+(?=s|sec|с|сек))")]
    private static partial Regex ParseRegex();
}
