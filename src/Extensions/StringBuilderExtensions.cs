using System.Text;

namespace Octobot.Extensions;

public static class StringBuilderExtensions
{
    public static StringBuilder AppendWithBullet(this StringBuilder builder, string? text)
    {
        return builder.Append("- ").Append(text);
    }

    public static StringBuilder AppendLineWithBullet(this StringBuilder builder, string? text)
    {
        return builder.Append("- ").AppendLine(text);
    }

    public static StringBuilder AppendLineWithSubBullet(this StringBuilder builder, string? text)
    {
        return builder.Append(" - ").AppendLine(text);
    }
}
