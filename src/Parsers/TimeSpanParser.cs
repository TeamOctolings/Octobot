using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Remora.Commands.Parsers;
using Remora.Results;

namespace Octobot.Parsers;

/// <summary>
///     Parses <see cref="TimeSpan"/> from <see cref="string"/>.
/// </summary>
/// <returns>
///     Parsed <see cref="TimeSpan"/>.
/// </returns>
/// <remarks>
///     If parse wasn't successful, <see cref="TimeSpanParser"/> will return <see cref="TimeSpan.Zero"/>.
/// </remarks>
[PublicAPI]
public partial class TimeSpanParser : AbstractTypeParser<TimeSpan>
{
    private static readonly Regex Pattern = ParseRegex();

    public static Result<TimeSpan> TryParse(string timeSpanString, CancellationToken ct = default)
    {
        var timeSpan = TimeSpan.Zero;
        timeSpanString = timeSpanString.Trim();

        if (timeSpanString.StartsWith('-'))
        {
            return timeSpan;
        }

        if (TimeSpan.TryParse(timeSpanString, DateTimeFormatInfo.InvariantInfo, out var parsedTimeSpan))
        {
            return parsedTimeSpan;
        }

        var matches = ParseRegex().Matches(timeSpanString);
        if (matches.Count is 0)
        {
            return timeSpan;
        }

        foreach (var groups in matches.Select(match => match.Groups
                     .Cast<Group>()
                     .Where(g => g.Success)
                     .Skip(1)
                     .Select(g => (g.Name, g.Value))))
        {
            foreach ((var key, var groupValue) in groups)
            {
                return !double.TryParse(groupValue, out var parsedGroupValue)
                    ? timeSpan
                    : ParseFromRegex(timeSpan, key, groupValue, parsedGroupValue);
            }
        }

        return timeSpan;
    }

    private static Result<TimeSpan> ParseFromRegex(TimeSpan timeSpan,
        string key, string groupValue, double parsedGroupValue)
    {
        if (key is "Years" or "Months")
        {
            if (!int.TryParse(groupValue, out var parsedIntegerValue))
            {
                return TimeSpan.Zero;
            }

            switch (key)
            {
                case "Years":
                {
                    var now = DateTimeOffset.UtcNow;
                    var then = now.AddYears(parsedIntegerValue);

                    timeSpan += then - now;
                    break;
                }
                case "Months":
                {
                    var now = DateTimeOffset.UtcNow;
                    var then = now.AddMonths(parsedIntegerValue);

                    timeSpan += then - now;
                    break;
                }
            }

            return timeSpan;
        }

        timeSpan += key switch
        {
            "Weeks" => TimeSpan.FromDays(parsedGroupValue * 7),
            "Days" => TimeSpan.FromDays(parsedGroupValue),
            "Hours" => TimeSpan.FromHours(parsedGroupValue),
            "Minutes" => TimeSpan.FromMinutes(parsedGroupValue),
            "Seconds" => TimeSpan.FromSeconds(parsedGroupValue),
            _ => throw new ArgumentOutOfRangeException(key)
        };

        return timeSpan;
    }

    [GeneratedRegex("(?<Years>\\d+(?=y|л|г))|(?<Months>\\d+(?=mo|мес))|(?<Weeks>\\d+(?=w|н|нед))|(?<Days>\\d+(?=d|дн))|(?<Hours>\\d+(?=h|ч))|(?<Minutes>\\d+(?=m|min|мин|м))|(?<Seconds>\\d+(?=s|sec|с|сек))")]
    private static partial Regex ParseRegex();
}
