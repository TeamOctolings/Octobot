using System.Text.Json.Nodes;
using Boyfriend.locale;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Data.Options;

public class SnowflakeOption : Option<Snowflake> {
    public SnowflakeOption(string name) : base(name, 0UL.ToSnowflake()) { }

    public override Snowflake Get(JsonNode settings) {
        var property = settings[Name];
        return property != null ? property.GetValue<ulong>().ToSnowflake() : DefaultValue;
    }

    public override Result Set(JsonNode settings, string from) {
        if (!ulong.TryParse(from, out var parsed))
            return Result.FromError(new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue));

        settings[Name] = parsed;
        return Result.FromSuccess();
    }
}
