using System.Globalization;
using System.Text.Json.Nodes;
using Boyfriend.locale;
using Remora.Results;

namespace Boyfriend.Data.Options;

/// <inheritdoc />
public class LanguageOption : Option<CultureInfo> {
    private static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "en", new CultureInfo("en-US") },
        { "ru", new CultureInfo("ru-RU") },
        { "mctaylors-ru", new CultureInfo("tt-RU") }
    };

    public LanguageOption(string name, string defaultValue) : base(name, CultureInfoCache[defaultValue]) { }

    /// <inheritdoc />
    public override CultureInfo Get(JsonNode settings) {
        var property = settings[Name];
        return property != null ? CultureInfoCache[property.GetValue<string>()] : DefaultValue;
    }

    /// <inheritdoc />
    public override Result Set(JsonNode settings, string from) {
        if (!CultureInfoCache.ContainsKey(from.ToLowerInvariant()))
            return Result.FromError(new ArgumentInvalidError(nameof(from), Messages.LanguageNotSupported));

        return base.Set(settings, from.ToLowerInvariant());
    }
}
