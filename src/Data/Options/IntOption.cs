using System.Text.Json.Nodes;
using Remora.Results;

namespace Octobot.Data.Options;

public sealed class IntOption : Option<int>
{
    public IntOption(string name, int defaultValue) : base(name, defaultValue) { }

    public override string Display(JsonNode settings)
    {
        return settings[Name]?.GetValue<string>() ?? "0";
    }

    public override Result Set(JsonNode settings, string from)
    {
        if (!int.TryParse(from, out _))
        {
            return new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue);
        }

        settings[Name] = from;
        return Result.FromSuccess();
    }

    public override int Get(JsonNode settings)
    {
        var property = settings[Name];
        return property != null ? Convert.ToInt32(property.GetValue<string>()) : DefaultValue;
    }
}
