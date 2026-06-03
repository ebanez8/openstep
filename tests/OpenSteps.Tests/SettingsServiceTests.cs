using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAndLoadAsync_PreservesScreenshotModeAsStringEnum()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var service = new SettingsService(path);

        await service.SaveAsync(new AppSettings { ScreenshotMode = ScreenshotMode.ActiveWindow });

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"screenshotMode\": \"ActiveWindow\"", json);

        var loaded = await service.LoadAsync();
        Assert.Equal(ScreenshotMode.ActiveWindow, loaded.ScreenshotMode);
    }

    [Fact]
    public async Task LoadAsync_ReturnsFullDesktopDefaultWhenSettingsFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"), "settings.json");

        var settings = await new SettingsService(path).LoadAsync();

        Assert.Equal(ScreenshotMode.FullDesktop, settings.ScreenshotMode);
    }
}
