using System.Net;
using Remora.Discord.Extensions.Formatting;

namespace TeamOctolings.Octobot.Extensions;

public static class StringExtensions
{
    private const string ZeroWidthSpace = "​";

    /// <summary>
    ///     Sanitizes a string for use in <see cref="Markdown.BlockCode(string)" /> by inserting zero-width spaces in between
    ///     symbols used to format the string with block code.
    /// </summary>
    /// <param name="s">The string to sanitize.</param>
    /// <returns>The sanitized string that can be safely used in <see cref="Markdown.BlockCode(string)" />.</returns>
    private static string SanitizeForBlockCode(this string s)
    {
        return s.Replace("```", $"{ZeroWidthSpace}`{ZeroWidthSpace}`{ZeroWidthSpace}`{ZeroWidthSpace}");
    }

    /// <summary>
    ///     Sanitizes a string for use in <see cref="Markdown.BlockCode(string, string)" /> when "language" is "diff" by
    ///     prepending a zero-width space before the input string to prevent Discord from applying syntax highlighting.
    /// </summary>
    /// <remarks>This does not call <see cref="SanitizeForBlockCode"/>, you have to do so yourself if needed.</remarks>
    /// <param name="s">The string to sanitize.</param>
    /// <returns>The sanitized string that can be safely used in <see cref="Markdown.BlockCode(string, string)" /> with "diff" as the language.</returns>
    public static string SanitizeForDiffBlock(this string s)
    {
        return $"{ZeroWidthSpace}{s}";
    }

    /// <summary>
    ///     Sanitizes a string (see <see cref="SanitizeForBlockCode" />) and formats the string to use Markdown Block Code
    ///     formatting with a specified
    ///     language for syntax highlighting.
    /// </summary>
    /// <param name="s">The string to sanitize and format.</param>
    /// <param name="language"></param>
    /// <returns>
    ///     The sanitized string formatted to use Markdown Block Code with a specified
    ///     language for syntax highlighting.
    /// </returns>
    public static string InBlockCode(this string s, string language = "")
    {
        s = s.SanitizeForBlockCode();
        return
            $"```{language}\n{s.SanitizeForBlockCode()}{(s.EndsWith('`') || string.IsNullOrWhiteSpace(s) ? " " : "")}```";
    }

    public static string Localized(this string key)
    {
        return Messages.ResourceManager.GetString(key, Messages.Culture) ?? key;
    }

    /// <summary>
    ///     Encodes a string to allow its transmission in request headers.
    /// </summary>
    /// <remarks>Used when encountering "Request headers must contain only ASCII characters".</remarks>
    /// <param name="s">The string to encode.</param>
    /// <returns>An encoded string with spaces kept intact.</returns>
    public static string EncodeHeader(this string s)
    {
        return WebUtility.UrlEncode(s).Replace('+', ' ');
    }
}
