using System.Text;
using DiffPlex.DiffBuilder.Model;

namespace TeamOctolings.Octobot.Extensions;

public static class DiffPaneModelExtensions
{
    public static string AsMarkdown(this DiffPaneModel model)
    {
        var builder = new StringBuilder();
        foreach (var line in model.Lines)
        {
            if (line.Type is ChangeType.Deleted)
            {
                builder.Append("-- ");
            }

            if (line.Type is ChangeType.Inserted)
            {
                builder.Append("++ ");
            }

            if (line.Type is not ChangeType.Imaginary)
            {
                builder.AppendLine(line.Text.SanitizeForDiffBlock());
            }
        }

        return builder.ToString().InBlockCode("diff");
    }
}
