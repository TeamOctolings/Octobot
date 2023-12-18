namespace Octobot.Extensions;

public static class MarkdownExtensions
{
    /// <summary>
    /// Formats a string to use Markdown Bullet formatting.
    /// </summary>
    /// <param name="text">The input text to format.</param>
    /// <returns>
    /// A markdown-formatted bullet string.
    /// </returns>
    public static string BulletPoint(string text)
    {
        return $"- {text}";
    }

    /// <summary>
    /// Formats a string to use Markdown URL formatting.
    /// </summary>
    /// <param name="text">The input text to format.</param>
    /// <param name="url">The URL to use in formatting.</param>
    /// <returns>
    /// A markdown-formatted string with URL.
    /// </returns>
    public static string Url(string text, string url)
    {
        return $"[{text}]({url})";
    }
}
