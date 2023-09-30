using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Data.Options;

public sealed partial class SnowflakeOption : Option<Snowflake>
{
    public SnowflakeOption(string name) : base(name, 0UL.ToSnowflake()) { }

    public override string Display(JsonNode settings)
    {
        return Name.EndsWith("Channel", StringComparison.Ordinal)
            ? Mention.Channel(Get(settings))
            : Mention.Role(Get(settings));
    }

    public override Snowflake Get(JsonNode settings)
    {
        var property = settings[Name];
        return property != null ? property.GetValue<ulong>().ToSnowflake() : DefaultValue;
    }

    public override Result Set(JsonNode settings, string from)
    {
        if (!ulong.TryParse(NonNumbers().Replace(from, ""), out var parsed))
        {
            return new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue);
        }

        settings[Name] = parsed;
        return Result.FromSuccess();
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NonNumbers();
}
