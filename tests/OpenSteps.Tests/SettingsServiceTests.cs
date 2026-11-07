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

    [Fact]
    public async Task SaveAndLoadAsync_PreservesThemeHotkeyAndMarkerSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var service = new SettingsService(path);

        await service.SaveAsync(new AppSettings
        {
            Theme = AppTheme.Dark,
            AccentColorMode = AccentColorMode.Custom,
            CustomAccentColor = "#107C10",
            RecordingHotkeyEnabled = false,
            RecordingHotkey = "Ctrl+Shift+F9",
            RecordKeyboardInput = false,
            ShowClickMarker = false,
            ClickMarkerStyle = ClickMarkerStyle.Filled,
            ClickMarkerColor = "#2563EB",
            ClickMarkerSize = ClickMarkerSize.Large
        });

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"theme\": \"Dark\"", json);

        var loaded = await service.LoadAsync();
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(AccentColorMode.Custom, loaded.AccentColorMode);
        Assert.Equal("#107C10", loaded.CustomAccentColor);
        Assert.False(loaded.RecordingHotkeyEnabled);
        Assert.Equal("Ctrl+Shift+F9", loaded.RecordingHotkey);
        Assert.False(loaded.RecordKeyboardInput);
        Assert.False(loaded.ShowClickMarker);
        Assert.Equal(ClickMarkerStyle.Filled, loaded.ClickMarkerStyle);
        Assert.Equal("#2563EB", loaded.ClickMarkerColor);
        Assert.Equal(ClickMarkerSize.Large, loaded.ClickMarkerSize);
    }

    [Fact]
    public async Task LoadAsync_FillsNewSettingsWithDefaultsForOlderSettingsFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ \"screenshotMode\": \"ActiveWindow\" }");

        var loaded = await new SettingsService(path).LoadAsync();

        Assert.Equal(ScreenshotMode.ActiveWindow, loaded.ScreenshotMode);
        Assert.Equal(AppTheme.System, loaded.Theme);
        Assert.Equal(AccentColorMode.System, loaded.AccentColorMode);
        Assert.True(loaded.RecordingHotkeyEnabled);
        Assert.Equal("Ctrl+Alt+R", loaded.RecordingHotkey);
        Assert.True(loaded.RecordKeyboardInput);
        Assert.True(loaded.ShowClickMarker);
    }
}
