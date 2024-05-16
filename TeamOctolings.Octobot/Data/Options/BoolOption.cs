using System.Text.Json.Nodes;
using Remora.Results;

namespace TeamOctolings.Octobot.Data.Options;

public sealed class BoolOption : Option<bool>
{
    public BoolOption(string name, bool defaultValue) : base(name, defaultValue) { }

    public override string Display(JsonNode settings)
    {
        return Get(settings) ? Messages.Yes : Messages.No;
    }

    public override Result Set(JsonNode settings, string from)
    {
        if (!TryParseBool(from, out var value))
        {
            return new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue);
        }

        settings[Name] = value;
        return Result.Success;
    }

    private static bool TryParseBool(string from, out bool value)
    {
        value = false;
        switch (from.ToLowerInvariant())
        {
            case "true" or "1" or "y" or "yes" or "д" or "да":
                value = true;
                return true;
            case "false" or "0" or "n" or "no" or "н" or "не" or "нет" or "нъет":
                value = false;
                return true;
            default:
                return false;
        }
    }
}
