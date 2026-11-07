using System.Drawing;
using System.Drawing.Imaging;
using OpenSteps.Capture;
using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class ClickMarkerTests
{
    [Fact]
    public void Render_DrawsCircleAroundClickAndLeavesSourceUntouched()
    {
        var temp = CreateTempDirectory();
        var source = Path.Combine(temp, "clean.png");
        var output = Path.Combine(temp, "marked.png");
        CreateWhiteImage(source);

        ClickMarkerRenderer.Render(source, output, 50, 50);

        using var marked = new Bitmap(output);
        Assert.NotEqual(Color.White.ToArgb(), marked.GetPixel(72, 50).ToArgb());
        Assert.Equal(Color.White.ToArgb(), marked.GetPixel(50, 50).ToArgb());
        using var clean = new Bitmap(source);
        Assert.Equal(Color.White.ToArgb(), clean.GetPixel(72, 50).ToArgb());
    }

    [Fact]
    public void Render_FilledStyleUsesConfiguredColorAndCoversCenter()
    {
        var temp = CreateTempDirectory();
        var source = Path.Combine(temp, "clean.png");
        var output = Path.Combine(temp, "marked.png");
        CreateWhiteImage(source);

        var previous = ClickMarkerRenderer.Appearance;
        try
        {
            ClickMarkerRenderer.Appearance = new ClickMarkerAppearance(ClickMarkerStyle.Filled, Color.FromArgb(37, 99, 235), ClickMarkerSize.Small);
            ClickMarkerRenderer.Render(source, output, 50, 50);
        }
        finally
        {
            ClickMarkerRenderer.Appearance = previous;
        }

        using var marked = new Bitmap(output);
        var center = marked.GetPixel(50, 50);
        Assert.True(center.B > center.R, "Filled marker should tint the click point with the configured color.");
        Assert.Equal(Color.White.ToArgb(), marked.GetPixel(72, 50).ToArgb());
    }

    [Fact]
    public void EffectiveScreenshotPath_UsesMarkedImageOnlyWhileMarkerIsShown()
    {
        var temp = CreateTempDirectory();
        var clean = WriteFile(temp, "clean.png");
        var marked = WriteFile(temp, "marked.png");
        var step = new RecordedStep
        {
            ScreenshotPath = clean,
            MarkedScreenshotPath = marked,
            ClickMarkerLayer = true,
            LocalClickX = 5,
            LocalClickY = 5
        };

        Assert.Equal(marked, step.EffectiveScreenshotPath);
        Assert.True(step.HasClickMarker);

        step.ShowClickMarker = false;

        Assert.Equal(clean, step.EffectiveScreenshotPath);
        Assert.True(step.HasClickMarker);
    }

    [Fact]
    public void EffectiveScreenshotPath_IgnoresMarkedImageForStepsWithoutMarkerLayer()
    {
        var temp = CreateTempDirectory();
        var clean = WriteFile(temp, "clean.png");
        var marked = WriteFile(temp, "marked.png");
        var step = new RecordedStep { ScreenshotPath = clean, MarkedScreenshotPath = marked, LocalClickX = 5, LocalClickY = 5 };

        Assert.Equal(clean, step.EffectiveScreenshotPath);
        Assert.False(step.HasClickMarker);
    }

    [Fact]
    public void BaseScreenshotPath_PrefersEditedImageAndNeverTheMarkedImage()
    {
        var temp = CreateTempDirectory();
        var step = new RecordedStep
        {
            ScreenshotPath = WriteFile(temp, "clean.png"),
            EditedScreenshotPath = WriteFile(temp, "edited.png"),
            MarkedScreenshotPath = WriteFile(temp, "marked.png"),
            ClickMarkerLayer = true
        };

        Assert.Equal(step.EditedScreenshotPath, step.BaseScreenshotPath);
    }

    [Fact]
    public void StepWithoutScreenshot_HasNoScreenshotAndNoMarker()
    {
        var step = new RecordedStep { ClickMarkerLayer = true, LocalClickX = 5, LocalClickY = 5 };

        Assert.False(step.HasScreenshot);
        Assert.False(step.HasClickMarker);
    }

    [Fact]
    public async Task SaveAndLoadSessionAsync_PreservesMarkedImageAndMarkerState()
    {
        var root = CreateTempDirectory();
        var session = new RecordingSession { Title = "Marker" };
        session.Steps.Add(new RecordedStep
        {
            Index = 1,
            ScreenshotPath = WriteFile(root, "capture.png"),
            MarkedScreenshotPath = WriteFile(root, "step-001-marked-1.png"),
            ClickMarkerLayer = true,
            ShowClickMarker = true,
            LocalClickX = 3,
            LocalClickY = 4
        });
        var store = new SessionStore(root);

        await store.SaveSessionAsync(session);
        var loaded = await store.LoadSessionAsync(session.Id);

        Assert.NotNull(loaded);
        var step = loaded.Steps[0];
        Assert.True(step.ClickMarkerLayer);
        Assert.Equal("images/step-001-marked-1.png", step.MarkedScreenshotRelativePath);
        Assert.True(File.Exists(step.MarkedScreenshotPath));
        Assert.Equal(step.MarkedScreenshotPath, step.EffectiveScreenshotPath);
    }

    [Fact]
    public async Task ExportAsync_ExportsMarkedImageWhenShownAndCleanImageWhenHidden()
    {
        var temp = CreateTempDirectory();
        var clean = Path.Combine(temp, "clean.png");
        var marked = Path.Combine(temp, "marked.png");
        await File.WriteAllBytesAsync(clean, [1, 1, 1]);
        await File.WriteAllBytesAsync(marked, [2, 2, 2]);
        var session = new RecordingSession { Title = "Layer" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Shown", ScreenshotPath = clean, MarkedScreenshotPath = marked, ClickMarkerLayer = true });
        session.Steps.Add(new RecordedStep { GeneratedTitle = "Hidden", ScreenshotPath = clean, MarkedScreenshotPath = marked, ClickMarkerLayer = true, ShowClickMarker = false });

        var result = await new MarkdownExporter().ExportAsync(session, Path.Combine(temp, "out"));

        Assert.Equal([2, 2, 2], await File.ReadAllBytesAsync(Path.Combine(result.ExportFolder, "images", "step-001.png")));
        Assert.Equal([1, 1, 1], await File.ReadAllBytesAsync(Path.Combine(result.ExportFolder, "images", "step-002.png")));
    }

    [Fact]
    public async Task ExportAsync_StepWithoutScreenshotExportsTextOnlyWithoutWarning()
    {
        var temp = CreateTempDirectory();
        var image = WriteFile(temp, "capture.png");
        var session = new RecordingSession { Title = "Text Only" };
        session.Steps.Add(new RecordedStep { GeneratedTitle = "No image", UserDescription = "Just read this." });
        session.Steps.Add(new RecordedStep { GeneratedTitle = "With image", ScreenshotPath = image });

        var result = await new MarkdownExporter().ExportAsync(session, Path.Combine(temp, "out"), GuideExportFormat.Markdown | GuideExportFormat.Html);

        var markdown = await File.ReadAllTextAsync(result.MarkdownPath);
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        Assert.Empty(result.Warnings);
        Assert.Contains("Just read this.", markdown);
        Assert.DoesNotContain("images/step-001.png", markdown);
        Assert.DoesNotContain("images/step-001.png", html);
        Assert.Contains("![Step 2](images/step-002.png)", markdown);
        Assert.False(File.Exists(Path.Combine(result.ExportFolder, "images", "step-001.png")));
    }

    private static string WriteFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private static void CreateWhiteImage(string path)
    {
        using var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
