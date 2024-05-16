using System.Text;

namespace TeamOctolings.Octobot.Extensions;

public static class StringBuilderExtensions
{
    /// <summary>
    ///     Appends the input string with Markdown Bullet formatting to the specified <see cref="StringBuilder" /> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder" /> object.</param>
    /// <param name="value">The string to append with bullet point.</param>
    /// <returns>
    ///     The builder with the appended string with Markdown Bullet formatting.
    /// </returns>
    public static StringBuilder AppendBulletPoint(this StringBuilder builder, string? value)
    {
        return builder.Append("- ").Append(value);
    }

    /// <summary>
    ///     Appends the input string with Markdown Sub-Bullet formatting to the specified <see cref="StringBuilder" /> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder" /> object.</param>
    /// <param name="value">The string to append with sub-bullet point.</param>
    /// <returns>
    ///     The builder with the appended string with Markdown Sub-Bullet formatting.
    /// </returns>
    public static StringBuilder AppendSubBulletPoint(this StringBuilder builder, string? value)
    {
        return builder.Append(" - ").Append(value);
    }

    /// <summary>
    ///     Appends the input string with Markdown Bullet formatting followed by
    ///     the default line terminator to the end of specified <see cref="StringBuilder" /> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder" /> object.</param>
    /// <param name="value">The string to append with bullet point.</param>
    /// <returns>
    ///     The builder with the appended string with Markdown Bullet formatting
    ///     and default line terminator at the end.
    /// </returns>
    public static StringBuilder AppendBulletPointLine(this StringBuilder builder, string? value)
    {
        return builder.Append("- ").AppendLine(value);
    }

    /// <summary>
    ///     Appends the input string with Markdown Sub-Bullet formatting followed by
    ///     the default line terminator to the end of specified <see cref="StringBuilder" /> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder" /> object.</param>
    /// <param name="value">The string to append with sub-bullet point.</param>
    /// <returns>
    ///     The builder with the appended string with Markdown Sub-Bullet formatting
    ///     and default line terminator at the end.
    /// </returns>
    public static StringBuilder AppendSubBulletPointLine(this StringBuilder builder, string? value)
    {
        return builder.Append(" - ").AppendLine(value);
    }
}
