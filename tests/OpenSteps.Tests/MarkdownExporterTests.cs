using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class MarkdownExporterTests
{
    [Fact]
    public void MarkdownBuilder_HandlesManualStepWithoutScreenshot()
    {
        var session = new RecordingSession { Title = "Manual Guide" };
        session.Steps.Add(new RecordedStep
        {
            ActionType = StepActionType.Manual,
            GeneratedTitle = "Manual step",
            UserTitle = "New step",
            UserDescription = "User description here."
        });

        var markdown = new MarkdownBuilder().BuildMarkdown(session);

        Assert.Contains("## Step 1: New step", markdown);
        Assert.Contains("User description here.", markdown);
        Assert.DoesNotContain("![Step 1]", markdown);
    }

    [Fact]
    public void MarkdownBuilder_UsesEditedTitleOverGeneratedTitle()
    {
        var session = new RecordingSession { Title = "Titles" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Generated", UserTitle = "Edited" });

        var markdown = new MarkdownBuilder().BuildMarkdown(session);

        Assert.Contains("## Step 1: Edited", markdown);
        Assert.DoesNotContain("## Step 1: Generated", markdown);
    }

    [Fact]
    public void MarkdownBuilder_RespectsCurrentStepOrder()
    {
        var session = new RecordingSession { Title = "Order Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Second" });
        session.Steps.Add(new RecordedStep { GeneratedTitle = "First" });
        session.Steps.Reverse();

        var markdown = new MarkdownBuilder().BuildMarkdown(session);

        Assert.True(markdown.IndexOf("## Step 1: First", StringComparison.Ordinal) < markdown.IndexOf("## Step 2: Second", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkdownBuilder_DoesNotEmitImageLinkForMissingScreenshot()
    {
        var session = new RecordingSession { Title = "Missing Screenshot" };
        session.Steps.Add(new RecordedStep
        {
            GeneratedTitle = "Missing",
            ScreenshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.png")
        });

        var markdown = new MarkdownBuilder().BuildMarkdown(session);

        Assert.Contains("## Step 1: Missing", markdown);
        Assert.DoesNotContain("images/step-001.png", markdown);
    }

    [Fact]
    public async Task ExportAsync_WritesGuideAndSequentialImagesInPortableFolder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);
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

        var result = await new MarkdownExporter().ExportAsync(session, temp);

        Assert.True(File.Exists(result.MarkdownPath));
        Assert.Equal(Path.Combine(temp, "Test Guide"), result.ExportFolder);
        Assert.True(File.Exists(Path.Combine(result.ExportFolder, "images", "step-001.png")));
        var markdown = await File.ReadAllTextAsync(result.MarkdownPath);
        Assert.Contains("# Test Guide", markdown);
        Assert.Contains("## Step 1: Open File menu", markdown);
        Assert.Contains("![Step 1](images/step-001.png)", markdown);
        Assert.DoesNotContain("AppData", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_CanWriteMarkdownAndHtmlFiles()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var session = new RecordingSession { Title = "Format Test" };
        session.Steps.Add(new RecordedStep
        {
            GeneratedTitle = "Generated",
            UserTitle = "Export formats",
            UserDescription = "Check the outputs."
        });

        var result = await new MarkdownExporter().ExportAsync(
            session,
            temp,
            GuideExportFormat.Markdown | GuideExportFormat.Html);

        Assert.True(File.Exists(result.MarkdownPath));
        Assert.True(File.Exists(result.HtmlPath));
        Assert.Contains(result.MarkdownPath, result.OutputPaths);
        Assert.Contains(result.HtmlPath, result.OutputPaths);
        Assert.Contains("<h2>Step 1: Export formats</h2>", await File.ReadAllTextAsync(result.HtmlPath));
    }

    [Fact]
    public async Task ExportAsync_CreatesUniqueFolderWhenTitleExists()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, "Conflict Test"));

        var session = new RecordingSession { Title = "Conflict Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Click" });

        var result = await new MarkdownExporter().ExportAsync(session, temp);

        Assert.Equal(Path.Combine(temp, "Conflict Test (1)"), result.ExportFolder);
        Assert.True(File.Exists(result.MarkdownPath));
    }

    [Fact]
    public async Task ExportAsync_WritesImagesInCurrentStepOrder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);

        var firstImage = Path.Combine(source, "first.png");
        var secondImage = Path.Combine(source, "second.png");
        await File.WriteAllBytesAsync(firstImage, [1]);
        await File.WriteAllBytesAsync(secondImage, [2]);

        var session = new RecordingSession { Title = "Image Order Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "First", ScreenshotPath = firstImage });
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Second", ScreenshotPath = secondImage });

        var result = await new MarkdownExporter().ExportAsync(session, temp);

        Assert.Equal([1], await File.ReadAllBytesAsync(Path.Combine(result.ExportFolder, "images", "step-001.png")));
        Assert.Equal([2], await File.ReadAllBytesAsync(Path.Combine(result.ExportFolder, "images", "step-002.png")));
    }

    [Fact]
    public async Task ExportAsync_UsesEditedScreenshotWhenAvailable()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);

        var original = Path.Combine(source, "original.png");
        var edited = Path.Combine(source, "edited.png");
        await File.WriteAllBytesAsync(original, [1]);
        await File.WriteAllBytesAsync(edited, [9]);

        var session = new RecordingSession { Title = "Edited Export Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Step", ScreenshotPath = original, EditedScreenshotPath = edited });

        var result = await new MarkdownExporter().ExportAsync(session, temp);

        Assert.Equal([9], await File.ReadAllBytesAsync(Path.Combine(result.ExportFolder, "images", "step-001.png")));
    }

    [Fact]
    public async Task ExportAsync_SkipsMissingScreenshotAndReturnsWarning()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);

        var original = Path.Combine(source, "missing.png");
        var session = new RecordingSession { Title = "Missing Export Test" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Step", ScreenshotPath = original });

        var result = await new MarkdownExporter().ExportAsync(session, temp);
        var markdown = await File.ReadAllTextAsync(result.MarkdownPath);

        Assert.Contains("Step 1 screenshot was missing", Assert.Single(result.Warnings));
        Assert.DoesNotContain("images/step-001.png", markdown);
    }

    [Fact]
    public void GetUniqueExportFolder_UsesTitleAndNumericSuffix()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, "Title"));

        var folder = new MarkdownExporter().GetUniqueExportFolder(temp, "Title");

        Assert.Equal(Path.Combine(temp, "Title (1)"), folder);
    }

    [Fact]
    public void ToSafeFolderName_RemovesInvalidFilenameCharacters()
    {
        var safe = new MarkdownExporter().ToSafeFolderName("Bad<>:\"/\\|?* Name");

        Assert.DoesNotContain('<', safe);
        Assert.DoesNotContain('>', safe);
        Assert.Contains("Bad", safe);
        Assert.Contains("Name", safe);
    }
}
