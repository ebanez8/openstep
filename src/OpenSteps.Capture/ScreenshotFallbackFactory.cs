using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public static class ScreenshotFallbackFactory
{
    public static ScreenshotCaptureResult WithFallbackMetadata(ScreenshotCaptureResult fullDesktopResult, ScreenshotMode requestedMode, string error)
    {
        return new ScreenshotCaptureResult
        {
            FilePath = fullDesktopResult.FilePath,
            CaptureMode = fullDesktopResult.CaptureMode,
            Monitor = fullDesktopResult.Monitor,
            CaptureLeft = fullDesktopResult.CaptureLeft,
            CaptureTop = fullDesktopResult.CaptureTop,
            CaptureRight = fullDesktopResult.CaptureRight,
            CaptureBottom = fullDesktopResult.CaptureBottom,
            LocalClickX = fullDesktopResult.LocalClickX,
            LocalClickY = fullDesktopResult.LocalClickY,
            ScreenshotWidth = fullDesktopResult.ScreenshotWidth,
            ScreenshotHeight = fullDesktopResult.ScreenshotHeight,
            RequestedScreenshotMode = requestedMode,
            ActualScreenshotMode = ScreenshotMode.FullDesktop,
            UsedScreenshotFallback = true,
            ScreenshotError = error,
            CapturedBounds = fullDesktopResult.CapturedBounds,
            GlobalClickX = fullDesktopResult.GlobalClickX,
            GlobalClickY = fullDesktopResult.GlobalClickY,
            HighlightX = fullDesktopResult.HighlightX,
            HighlightY = fullDesktopResult.HighlightY,
            HighlightWasInsideCapturedBounds = fullDesktopResult.HighlightWasInsideCapturedBounds,
            BoundsSource = fullDesktopResult.BoundsSource
        };
    }
}
