using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class ScreenshotCaptureResult
{
    public required string FilePath { get; init; }

    public ScreenshotCaptureMode CaptureMode { get; init; }

    public DisplayMonitorInfo? Monitor { get; init; }

    public int CaptureLeft { get; init; }

    public int CaptureTop { get; init; }

    public int CaptureRight { get; init; }

    public int CaptureBottom { get; init; }

    public int LocalClickX { get; init; }

    public int LocalClickY { get; init; }

    public int ScreenshotWidth { get; init; }

    public int ScreenshotHeight { get; init; }

    public ScreenshotMode RequestedScreenshotMode { get; init; }

    public ScreenshotMode ActualScreenshotMode { get; init; }

    public bool UsedScreenshotFallback { get; init; }

    public string? ScreenshotError { get; init; }

    public ScreenBounds? CapturedBounds { get; init; }

    public int GlobalClickX { get; init; }

    public int GlobalClickY { get; init; }

    public int? HighlightX { get; init; }

    public int? HighlightY { get; init; }

    public bool HighlightWasInsideCapturedBounds { get; init; }

    public string? BoundsSource { get; init; }
}
