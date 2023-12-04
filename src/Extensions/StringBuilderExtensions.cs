using System.Text;

namespace Octobot.Extensions;

public static class StringBuilderExtensions
{
    public static StringBuilder AppendBulletPoint(this StringBuilder builder, string? text)
    {
        return builder.Append("- ").Append(text);
    }

    public static StringBuilder AppendSubBulletPoint(this StringBuilder builder, string? text)
    {
        return builder.Append(" - ").Append(text);
    }

    public static StringBuilder AppendBulletPointLine(this StringBuilder builder, string? text)
    {
        return builder.Append("- ").AppendLine(text);
    }

    public static StringBuilder AppendSubBulletPointLine(this StringBuilder builder, string? text)
    {
        return builder.Append(" - ").AppendLine(text);
    }
}
