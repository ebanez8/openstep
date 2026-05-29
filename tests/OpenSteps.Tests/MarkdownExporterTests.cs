using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class MarkdownExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesGuideAndSequentialImages()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source");
        var export = Path.Combine(temp, "export");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(export);
        var image = Path.Combine(source, "capture.png");
        await File.WriteAllBytesAsync(image, [1, 2, 3]);

        var session = new RecordingSession { Title = "Test Guide" };
        session.Steps.Add(new RecordedStep
        {
            GeneratedTitle = "Click \"File\"",
            UserTitle = "Open File menu",
            UserDescription = "Choose the main menu.",
            ScreenshotPath = image
        });

        var guidePath = await new MarkdownExporter().ExportAsync(session, export);

        Assert.True(File.Exists(guidePath));
        Assert.True(File.Exists(Path.Combine(export, "images", "step-001.png")));
        var markdown = await File.ReadAllTextAsync(guidePath);
        Assert.Contains("# Test Guide", markdown);
        Assert.Contains("## Step 1: Open File menu", markdown);
        Assert.Contains("![Step 1](images/step-001.png)", markdown);
        Assert.Contains("Choose the main menu.", markdown);
    }
}
