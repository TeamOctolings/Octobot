using System.Text.Json.Nodes;
using Remora.Results;
using TeamOctolings.Octobot.Parsers;

namespace TeamOctolings.Octobot.Data.Options;

public sealed class TimeSpanOption : GuildOption<TimeSpan>
{
    public TimeSpanOption(string name, TimeSpan defaultValue) : base(name, defaultValue) { }

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
