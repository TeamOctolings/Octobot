using System.Text.Json.Nodes;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

namespace Octobot.Data.Options;

/// <summary>
///     Represents an per-guild option.
/// </summary>
/// <typeparam name="T">The type of the option.</typeparam>
public class Option<T> : IOption
    where T : notnull
{
    protected readonly T DefaultValue;

    public Option(string name, T defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }

    public virtual string Display(JsonNode settings)
    {
        return Markdown.InlineCode(Get(settings).ToString() ?? throw new InvalidOperationException());
    }

    /// <summary>
    ///     Sets the value of the option from a <see cref="string" /> to the provided JsonNode.
    /// </summary>
    /// <param name="settings">The <see cref="JsonNode" /> to set the value to.</param>
    /// <param name="from">The string from which the new value of the option will be parsed.</param>
    /// <returns>A value setting result which may or may not have succeeded.</returns>
    public virtual Result Set(JsonNode settings, string from)
    {
        settings[Name] = from;
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Gets the value of the option from the provided <paramref name="settings" />.
    /// </summary>
    /// <param name="settings">The <see cref="JsonNode" /> to get the value from.</param>
    /// <returns>The value of the option.</returns>
    public virtual T Get(JsonNode settings)
    {
        var property = settings[Name];
        return property != null ? property.GetValue<T>() : DefaultValue;
    }

    public Result Reset(JsonNode settings)
    {
        settings[Name] = null;
        return Result.FromSuccess();
    }
}
