using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task SaveAndLoadLatestAsync_PreservesEditedTextAndImages()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var image = Path.Combine(source, "capture.png");
        var editedImage = Path.Combine(source, "capture-redacted.png");
        await File.WriteAllBytesAsync(image, [4, 5, 6]);
        await File.WriteAllBytesAsync(editedImage, [7, 8, 9]);

        var session = new RecordingSession { Title = "Saved Session" };
        session.Steps.Add(new RecordedStep
        {
            Index = 1,
            ScreenshotPath = image,
            GeneratedTitle = "Generated",
            UserTitle = "Edited title",
            UserDescription = "Edited description",
            ScreenshotCaptured = true,
            EditedScreenshotPath = editedImage,
            Redactions = [new RedactionRegion(1, 2, 3, 4, RedactionMode.Pixelate)]
        });

        var store = new SessionStore(root);
        await store.SaveAsync(session);

        var loaded = await store.LoadLatestAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Saved Session", loaded.Title);
        Assert.Single(loaded.Steps);
        Assert.Equal("Edited title", loaded.Steps[0].UserTitle);
        Assert.Equal("Edited description", loaded.Steps[0].UserDescription);
        Assert.True(File.Exists(loaded.Steps[0].ScreenshotPath));
        Assert.True(File.Exists(loaded.Steps[0].EditedScreenshotPath));
        Assert.Single(loaded.Steps[0].Redactions);
    }
}
