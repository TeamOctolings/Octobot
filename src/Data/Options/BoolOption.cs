using System.Text.Json.Nodes;
using Remora.Results;

namespace Boyfriend.Data.Options;

public class BoolOption : Option<bool> {
    public BoolOption(string name, bool defaultValue) : base(name, defaultValue) { }

    public override string Display(JsonNode settings) {
        return Get(settings) ? Messages.Yes : Messages.No;
    }

    public override Result Set(JsonNode settings, string from) {
        if (!TryParseBool(from, out var value))
            return Result.FromError(new ArgumentInvalidError(nameof(from), Messages.InvalidSettingValue));

        settings[Name] = value;
        return Result.FromSuccess();
    }

    private static bool TryParseBool(string from, out bool value) {
        value = false;
        switch (from) {
            case "1" or "y" or "yes" or "д" or "да":
                value = true;
                return true;
            case "0" or "n" or "no" or "н" or "не" or "нет":
                value = false;
                return true;
            default:
                return false;
        }
    }
}
