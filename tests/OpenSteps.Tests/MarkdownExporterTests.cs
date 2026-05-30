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

    [Fact]
    public async Task ExportAsync_UsesAvailableFolderWhenGuideAlreadyExists()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var export = Path.Combine(temp, "export");
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, "guide.md"), "existing");

        var session = new RecordingSession { Title = "Conflict Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Click" });

        var guidePath = await new MarkdownExporter().ExportAsync(session, export);

        Assert.NotEqual(Path.Combine(export, "guide.md"), guidePath);
        Assert.EndsWith(Path.Combine("export-001", "guide.md"), guidePath);
        Assert.True(File.Exists(guidePath));
    }

    [Fact]
    public async Task ExportAsync_UsesCurrentStepOrder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var session = new RecordingSession { Title = "Order Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Second" });
        session.Steps.Add(new RecordedStep { GeneratedTitle = "First" });
        session.Steps.Reverse();

        var guidePath = await new MarkdownExporter().ExportAsync(session, temp);
        var markdown = await File.ReadAllTextAsync(guidePath);

        Assert.True(markdown.IndexOf("## Step 1: First", StringComparison.Ordinal) < markdown.IndexOf("## Step 2: Second", StringComparison.Ordinal));
    }
}
