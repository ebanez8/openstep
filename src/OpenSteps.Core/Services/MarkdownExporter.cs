using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class MarkdownExporter
{
    private readonly MarkdownBuilder _markdownBuilder;

    public MarkdownExporter(MarkdownBuilder? markdownBuilder = null)
    {
        _markdownBuilder = markdownBuilder ?? new MarkdownBuilder();
    }

    public string GetUniqueExportFolder(string parentFolder, string sessionTitle)
    {
        var safeName = ToSafeFolderName(sessionTitle);
        var candidate = Path.Combine(parentFolder, safeName);
        if (!Directory.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 1000; i++)
        {
            candidate = Path.Combine(parentFolder, $"{safeName} ({i})");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(parentFolder, $"{safeName} ({DateTimeOffset.Now:yyyyMMdd-HHmmss})");
    }

    public string ToSafeFolderName(string title)
    {
        var safe = string.Join(string.Empty, (title ?? string.Empty).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch)).Trim();
        safe = string.Join(" ", safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (safe.Length > 80)
        {
            safe = safe[..80].Trim();
        }

        return string.IsNullOrWhiteSpace(safe) ? "OpenSteps Guide" : safe;
    }

    public async Task<ExportResult> ExportAsync(RecordingSession session, string parentFolder, CancellationToken cancellationToken = default)
    {
        var exportFolder = GetUniqueExportFolder(parentFolder, session.Title);
        Directory.CreateDirectory(exportFolder);
        var imageDirectory = Path.Combine(exportFolder, "images");
        Directory.CreateDirectory(imageDirectory);

        var warnings = new List<string>();
        for (var i = 0; i < session.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = session.Steps[i];
            var screenshotPath = GetExportScreenshotPath(step);
            if (string.IsNullOrWhiteSpace(screenshotPath))
            {
                continue;
            }

            if (!File.Exists(screenshotPath))
            {
                warnings.Add($"Step {i + 1} screenshot was missing and was skipped.");
                continue;
            }

            var destination = Path.Combine(imageDirectory, $"step-{i + 1:000}.png");
            File.Copy(screenshotPath, destination, overwrite: false);
        }

        var markdown = _markdownBuilder.BuildMarkdown(session);
        var guidePath = Path.Combine(exportFolder, "guide.md");
        await File.WriteAllTextAsync(guidePath, markdown, cancellationToken);

        return new ExportResult
        {
            ExportFolder = exportFolder,
            MarkdownPath = guidePath,
            Warnings = warnings
        };
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
