namespace TeamOctolings.Octobot.Extensions;

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
    /// Formats a string to use Markdown Quote formatting.
    /// </summary>
    /// <param name="text">The input text to format.</param>
    /// <returns>
    /// A markdown-formatted quote string.
    /// </returns>
    public static string Quote(string text)
    {
        return $"> {text}";
    }
}
