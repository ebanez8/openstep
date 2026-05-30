using System.Text;
using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class MarkdownExporter
{
    public string GetAvailableExportDirectory(string requestedDirectory)
    {
        if (!Directory.Exists(requestedDirectory))
        {
            return requestedDirectory;
        }

        var hasGuide = File.Exists(Path.Combine(requestedDirectory, "guide.md"));
        var hasImages = Directory.Exists(Path.Combine(requestedDirectory, "images"));
        if (!hasGuide && !hasImages)
        {
            return requestedDirectory;
        }

        var parent = Directory.GetParent(requestedDirectory)?.FullName ?? requestedDirectory;
        var name = Path.GetFileName(requestedDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "OpenStepsExport";
        }

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(parent, $"{name}-{i:000}");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(parent, $"{name}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
    }

    public async Task<string> ExportAsync(RecordingSession session, string exportDirectory, CancellationToken cancellationToken = default)
    {
        exportDirectory = GetAvailableExportDirectory(exportDirectory);
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
