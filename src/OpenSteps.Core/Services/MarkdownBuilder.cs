using System.Text;
using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class MarkdownBuilder
{
    public string BuildMarkdown(RecordingSession session)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine($"# {EscapeHeading(session.Title)}");
        markdown.AppendLine();
        markdown.AppendLine("Created with OpenSteps.");
        markdown.AppendLine();

        for (var i = 0; i < session.Steps.Count; i++)
        {
            var step = session.Steps[i];
            var number = i + 1;
            var title = string.IsNullOrWhiteSpace(step.DisplayTitle) ? $"Step {number}" : step.DisplayTitle;
            markdown.AppendLine($"## Step {number}: {title}");
            markdown.AppendLine();

            var screenshotPath = GetExportScreenshotPath(step);
            if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
            {
                markdown.AppendLine($"![Step {number}](images/step-{number:000}.png)");
                markdown.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(step.UserDescription))
            {
                markdown.AppendLine(step.UserDescription.Trim());
                markdown.AppendLine();
            }
        }

        return markdown.ToString();
    }

    private static string EscapeHeading(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Untitled OpenSteps Guide" : value.Trim();
    }

    private static string? GetExportScreenshotPath(RecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.EditedScreenshotPath) && File.Exists(step.EditedScreenshotPath))
        {
            return step.EditedScreenshotPath;
        }

        return step.ScreenshotPath;
    }
}
