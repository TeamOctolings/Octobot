using JetBrains.Annotations;
using Remora.Commands.Parsers;
using Remora.Results;

namespace Octobot.Parsers;

/// <summary>
/// Parses <see cref="TimeSpan"/>.
/// </summary>
[PublicAPI]
public class TimeSpanParser : AbstractTypeParser<TimeSpan>
{
    public static Result<TimeSpan> TryParse(string timeSpanString, CancellationToken ct = default)
    {
        if (timeSpanString.StartsWith('-'))
        {
            return TimeSpan.Zero;
        }

        var parser = new Remora.Commands.Parsers.TimeSpanParser();
        var parseResult = parser.TryParseAsync(timeSpanString, ct).AsTask().GetAwaiter().GetResult();
        return !parseResult.IsDefined(out var @in) ? TimeSpan.Zero : @in;
    }
}
