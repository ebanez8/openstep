using System.Text;
using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class MarkdownExporter
{
    public async Task<string> ExportAsync(RecordingSession session, string exportDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(exportDirectory);
        var imageDirectory = Path.Combine(exportDirectory, "images");
        Directory.CreateDirectory(imageDirectory);

        var markdown = new StringBuilder();
        markdown.AppendLine($"# {EscapeHeading(session.Title)}");
        markdown.AppendLine();
        markdown.AppendLine("Created with OpenSteps.");
        markdown.AppendLine();

        for (var i = 0; i < session.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = session.Steps[i];
            var number = i + 1;
            var title = string.IsNullOrWhiteSpace(step.DisplayTitle) ? $"Step {number}" : step.DisplayTitle;
            markdown.AppendLine($"## Step {number}: {title}");
            markdown.AppendLine();

            if (!string.IsNullOrWhiteSpace(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
            {
                var imageName = $"step-{number:000}.png";
                var destination = Path.Combine(imageDirectory, imageName);
                File.Copy(step.ScreenshotPath, destination, overwrite: true);
                markdown.AppendLine($"![Step {number}](images/{imageName})");
                markdown.AppendLine();
            }
            else if (!string.IsNullOrWhiteSpace(step.CaptureError))
            {
                markdown.AppendLine($"> Screenshot unavailable: {step.CaptureError}");
                markdown.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(step.UserDescription))
            {
                markdown.AppendLine(step.UserDescription.Trim());
                markdown.AppendLine();
            }
        }

        var guidePath = Path.Combine(exportDirectory, "guide.md");
        await File.WriteAllTextAsync(guidePath, markdown.ToString(), cancellationToken);
        return guidePath;
    }

    private static string EscapeHeading(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Untitled OpenSteps Guide" : value.Trim();
    }
}
