using System.Text.Json.Nodes;
using Remora.Results;
using TeamOctolings.Octobot.Parsers;

namespace TeamOctolings.Octobot.Data.Options;

public sealed class TimeSpanOption : GuildOption<TimeSpan>
{
    public TimeSpanOption(string name, TimeSpan defaultValue) : base(name, defaultValue) { }

    public override Result<bool> ValueEquals(JsonNode settings, string value)
    {
        if (!TimeSpanParser.TryParse(value).IsDefined(out var span))
        {
            return new ArgumentInvalidError(nameof(value), Messages.InvalidSettingValue);
        }

        return Value(settings).Equals(span.ToString());
    }

    public override TimeSpan Get(JsonNode settings)
    {
        var property = settings[Name];
        return property != null ? TimeSpanParser.TryParse(property.GetValue<string>()).Entity : DefaultValue;
    }

    public override Result Set(JsonNode settings, string from)
    {
        if (!TimeSpanParser.TryParse(from).IsDefined(out var span))
        {
            return new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue);
        }

        settings[Name] = span.ToString();
        return Result.Success;
    }
}
