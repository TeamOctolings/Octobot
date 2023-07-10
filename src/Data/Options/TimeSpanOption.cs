using System.Text.Json.Nodes;
using Boyfriend.locale;
using Remora.Commands.Parsers;
using Remora.Results;

namespace Boyfriend.Data.Options;

public class TimeSpanOption : Option<TimeSpan> {
    private static readonly TimeSpanParser Parser = new();

    public TimeSpanOption(string name, TimeSpan defaultValue) : base(name, defaultValue) { }

    public override Result Set(JsonNode settings, string from) {
        var task = Parser.TryParseAsync(from).AsTask().GetAwaiter().GetResult();

        if (task.IsDefined(out var span))
            return Result.FromError(new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue));

        settings[Name] = span.ToString();
        return Result.FromSuccess();
    }
}
