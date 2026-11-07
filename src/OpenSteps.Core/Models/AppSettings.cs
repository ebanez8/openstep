namespace OpenSteps.Core.Models;

public sealed class AppSettings
{
    /// <summary>Capture mode the floating bar starts with on each launch.</summary>
    public ScreenshotMode ScreenshotMode { get; set; } = ScreenshotMode.FullDesktop;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public AccentColorMode AccentColorMode { get; set; } = AccentColorMode.System;

    public string CustomAccentColor { get; set; } = "#0078D4";

    public bool RecordingHotkeyEnabled { get; set; } = true;

    public string RecordingHotkey { get; set; } = "Ctrl+Alt+R";

    public bool RecordKeyboardInput { get; set; } = true;

    public bool ShowClickMarker { get; set; } = true;

    public ClickMarkerStyle ClickMarkerStyle { get; set; } = ClickMarkerStyle.Ring;

    public string ClickMarkerColor { get; set; } = "#DC1E1E";

    public ClickMarkerSize ClickMarkerSize { get; set; } = ClickMarkerSize.Medium;
}
