using System.Drawing;

namespace Octobot;

/// <summary>
///     Contains all colors used in embeds.
/// </summary>
public static class ColorsList
{
    public static readonly Color Default = Color.Gray;
    public static readonly Color Red = Color.Firebrick;
    public static readonly Color Green = Color.PaleGreen;
    public static readonly Color Yellow = Color.Gold;
    public static readonly Color Blue = Color.RoyalBlue;
    public static readonly Color Magenta = Color.Orchid;
    public static readonly Color Cyan = Color.LightSkyBlue;
    public static readonly Color Black = Color.Black;
    public static readonly Color White = Color.WhiteSmoke;
}

/// <summary>
///     Contains all links used in buttons.
/// </summary>
public static class Links
{
    public const string Repository = "https://github.com/LabsDevelopment/Octobot";
    public const string Issues = $"{Repository}/issues";
}
