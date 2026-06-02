using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task SaveAndLoadSessionAsync_PreservesEditedTextAndImages()
    {
        var root = CreateRoot();
        var image = await CreateSourceFileAsync(root, "capture.png", [4, 5, 6]);
        var editedImage = await CreateSourceFileAsync(root, "capture-redacted.png", [7, 8, 9]);

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
        await store.SaveSessionAsync(session);

        var loaded = await store.LoadSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Saved Session", loaded.Title);
        Assert.Equal(root, store.RootDirectory);
        Assert.Single(loaded.Steps);
        Assert.Equal("Edited title", loaded.Steps[0].UserTitle);
        Assert.Equal("Edited description", loaded.Steps[0].UserDescription);
        Assert.Equal("images/step-001.png", loaded.Steps[0].ScreenshotRelativePath);
        Assert.Equal("images/step-001-redacted.png", loaded.Steps[0].EditedScreenshotRelativePath);
        Assert.True(File.Exists(loaded.Steps[0].ScreenshotPath));
        Assert.True(File.Exists(loaded.Steps[0].EditedScreenshotPath));
        Assert.Single(loaded.Steps[0].Redactions);
    }

    [Fact]
    public async Task SaveSessionAsync_WritesRelativePathsInJson()
    {
        var root = CreateRoot();
        var image = await CreateSourceFileAsync(root, "capture.png", [1]);
        var session = new RecordingSession { Title = "Relative Paths" };
        session.Steps.Add(new RecordedStep { ScreenshotPath = image, GeneratedTitle = "Step" });

        await new SessionStore(root).SaveSessionAsync(session);

        var json = await File.ReadAllTextAsync(Path.Combine(root, session.Id.ToString("N"), "session.json"));
        Assert.Contains("\"screenshotRelativePath\": \"images/step-001.png\"", json);
        Assert.DoesNotContain("screenshotPath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(image.Replace("\\", "\\\\"), json);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsUpdatedDescending()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var older = new RecordingSession { Title = "Older" };
        var newer = new RecordingSession { Title = "Newer" };

        await store.SaveSessionAsync(older);
        await Task.Delay(20);
        await store.SaveSessionAsync(newer);

        var sessions = await store.ListSessionsAsync();

        Assert.Equal([newer.Id, older.Id], sessions.Select(session => session.Id).ToArray());
    }

    [Fact]
    public async Task RenameSessionAsync_UpdatesTitleAndUpdatedAt()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var session = new RecordingSession { Title = "Before" };
        await store.SaveSessionAsync(session);
        var before = (await store.LoadSessionAsync(session.Id))!.UpdatedAt;

        await Task.Delay(20);
        await store.RenameSessionAsync(session.Id, "After");
        var loaded = await store.LoadSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal("After", loaded.Title);
        Assert.True(loaded.UpdatedAt > before);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSessionFolder()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var session = new RecordingSession { Title = "Delete Me" };
        await store.SaveSessionAsync(session);

        await store.DeleteSessionAsync(session.Id);

        Assert.False(Directory.Exists(Path.Combine(root, session.Id.ToString("N"))));
    }

    [Fact]
    public async Task LoadSessionAsync_ReturnsNullForMissingOrCorruptSession()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var corruptId = Guid.NewGuid();
        var corruptDirectory = Path.Combine(root, corruptId.ToString("N"));
        Directory.CreateDirectory(corruptDirectory);
        await File.WriteAllTextAsync(Path.Combine(corruptDirectory, "session.json"), "{ not json");

        Assert.Null(await store.LoadSessionAsync(Guid.NewGuid()));
        Assert.Null(await store.LoadSessionAsync(corruptId));
    }

    [Fact]
    public async Task LoadSessionAsync_RestoresLegacyAbsoluteScreenshotPath()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var sessionId = Guid.NewGuid();
        var sessionDirectory = Path.Combine(root, sessionId.ToString("N"));
        Directory.CreateDirectory(sessionDirectory);
        var image = await CreateSourceFileAsync(root, "legacy.png", [1, 2, 3]);
        var json = $$"""
        {
          "schemaVersion": 1,
          "id": "{{sessionId}}",
          "title": "Legacy",
          "createdAt": "2026-06-01T22:10:00Z",
          "updatedAt": "2026-06-01T22:15:00Z",
          "steps": [
            {
              "id": "{{Guid.NewGuid()}}",
              "index": 1,
              "screenshotPath": "{{image.Replace("\\", "\\\\")}}",
              "generatedTitle": "Step"
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(sessionDirectory, "session.json"), json);

        var loaded = await store.LoadSessionAsync(sessionId);

        Assert.NotNull(loaded);
        Assert.Equal(image, loaded.Steps[0].ScreenshotPath);
        Assert.Equal(image, loaded.Steps[0].EffectiveScreenshotPath);
    }

    [Fact]
    public void EffectiveScreenshotPath_FallsBackToOriginalWhenEditedImageIsMissing()
    {
        var root = CreateRoot();
        var original = Path.Combine(root, "original.png");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(original, [1]);

        var step = new RecordedStep
        {
            ScreenshotPath = original,
            EditedScreenshotPath = Path.Combine(root, "missing-redacted.png")
        };

        Assert.Equal(original, step.EffectiveScreenshotPath);
    }

    [Fact]
    public async Task ListSessionsAsync_UsesOnlyExistingThumbnailFiles()
    {
        var root = CreateRoot();
        var store = new SessionStore(root);
        var image = await CreateSourceFileAsync(root, "existing.png", [1, 2, 3]);
        var session = new RecordingSession { Title = "Thumbnail" };
        session.Steps.Add(new RecordedStep
        {
            GeneratedTitle = "Missing",
            ScreenshotRelativePath = "images/missing.png"
        });
        session.Steps.Add(new RecordedStep
        {
            GeneratedTitle = "Existing",
            ScreenshotPath = image
        });

        await store.SaveSessionAsync(session);

        var summary = Assert.Single(await store.ListSessionsAsync());
        Assert.True(File.Exists(summary.ThumbnailPath));
    }

    private static string CreateRoot()
    {
        return Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
    }

    private static async Task<string> CreateSourceFileAsync(string root, string name, byte[] bytes)
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var path = Path.Combine(source, name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}
