using OpenSteps.Core.Models;
using System.Text;
using System.Text.Encodings.Web;

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
        return await ExportAsync(session, parentFolder, GuideExportFormat.Markdown, cancellationToken);
    }

    public async Task<ExportResult> ExportAsync(RecordingSession session, string parentFolder, GuideExportFormat formats, CancellationToken cancellationToken = default)
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

        var outputPaths = new List<string>();
        var markdownPath = string.Empty;
        var htmlPath = string.Empty;

        if (formats.HasFlag(GuideExportFormat.Markdown))
        {
            var markdown = _markdownBuilder.BuildMarkdown(session);
            markdownPath = Path.Combine(exportFolder, "guide.md");
            await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
            outputPaths.Add(markdownPath);
        }

        if (formats.HasFlag(GuideExportFormat.Html))
        {
            htmlPath = Path.Combine(exportFolder, "guide.html");
            await File.WriteAllTextAsync(htmlPath, BuildHtml(session), cancellationToken);
            outputPaths.Add(htmlPath);
        }

        return new ExportResult
        {
            ExportFolder = exportFolder,
            MarkdownPath = markdownPath,
            HtmlPath = htmlPath,
            OutputPaths = outputPaths,
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

    private static string BuildHtml(RecordingSession session)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{Encode(session.Title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;line-height:1.55;color:#18232b;background:#f8fcfd;margin:0;padding:40px}");
        html.AppendLine("main{max-width:900px;margin:0 auto;background:#fff;border:1px solid #d7e7ed;border-radius:14px;padding:34px}");
        html.AppendLine("h1{margin-top:0;font-size:32px} h2{margin-top:34px;border-top:1px solid #d7e7ed;padding-top:24px}");
        html.AppendLine("img{max-width:100%;border:1px solid #d7e7ed;border-radius:10px;background:#eef7fa}");
        html.AppendLine("p{white-space:pre-wrap}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body><main>");
        html.AppendLine($"<h1>{Encode(string.IsNullOrWhiteSpace(session.Title) ? "Untitled OpenSteps Guide" : session.Title.Trim())}</h1>");
        html.AppendLine("<p>Created with OpenSteps.</p>");

        for (var i = 0; i < session.Steps.Count; i++)
        {
            var step = session.Steps[i];
            var number = i + 1;
            var title = string.IsNullOrWhiteSpace(step.DisplayTitle) ? $"Step {number}" : step.DisplayTitle;
            html.AppendLine($"<h2>Step {number}: {Encode(title)}</h2>");
            var screenshotPath = GetExportScreenshotPath(step);
            if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
            {
                html.AppendLine($"<p><img src=\"images/step-{number:000}.png\" alt=\"Step {number}\"></p>");
            }

            if (!string.IsNullOrWhiteSpace(step.UserDescription))
            {
                html.AppendLine($"<p>{Encode(step.UserDescription.Trim())}</p>");
            }
        }

        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static string Encode(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }
}
