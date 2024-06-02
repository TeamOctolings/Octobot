using System.Text.Json.Nodes;
using Remora.Results;

namespace TeamOctolings.Octobot.Data.Options;

/// <inheritdoc />
public sealed class PunishmentOption : GuildOption<string>
{
    private static readonly List<string> AllowedValues =
    [
        "ban", "kick", "mute", "off", "disable", "disabled"
    ];

    public PunishmentOption(string name, string defaultValue) : base(name, defaultValue) { }

    /// <inheritdoc />
    public override Result Set(JsonNode settings, string from)
    {
        return AllowedValues.Contains(from.ToLowerInvariant())
            ? base.Set(settings, from.ToLowerInvariant())
            : new ArgumentInvalidError(nameof(from), Messages.InvalidWarnPunishment);
    }
}
