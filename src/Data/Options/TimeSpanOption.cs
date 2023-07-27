using System.Text.Json.Nodes;
using Remora.Commands.Parsers;
using Remora.Results;

namespace Boyfriend.Data.Options;

public class TimeSpanOption : Option<TimeSpan> {
    private static readonly TimeSpanParser Parser = new();

    public TimeSpanOption(string name, TimeSpan defaultValue) : base(name, defaultValue) { }

    public override TimeSpan Get(JsonNode settings) {
        var property = settings[Name];
        return property != null ? ParseTimeSpan(property.GetValue<string>()).Entity : DefaultValue;
    }

    public override Result Set(JsonNode settings, string from) {
        if (!ParseTimeSpan(from).IsDefined(out var span))
            return new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue);

        settings[Name] = span.ToString();
        return Result.FromSuccess();
    }

    private static Result<TimeSpan> ParseTimeSpan(string from) {
        return Parser.TryParseAsync(from).AsTask().GetAwaiter().GetResult();
    }
}
