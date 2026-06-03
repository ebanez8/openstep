using OpenSteps.Capture;
using OpenSteps.Core.Models;

namespace OpenSteps.Tests;

public sealed class ScreenshotMetadataTests
{
    [Fact]
    public void ToCapturedPoint_ConvertsGlobalClickToRelativeHighlight()
    {
        var point = ScreenshotCoordinateMapper.ToCapturedPoint(1420, 277, 1000, 200);

        Assert.Equal((420, 77), point);
    }

    [Fact]
    public void IsInsideCapturedBounds_ReturnsFalseForOutsideHighlight()
    {
        Assert.False(ScreenshotCoordinateMapper.IsInsideCapturedBounds(420, 77, 400, 100));
        Assert.False(ScreenshotCoordinateMapper.IsInsideCapturedBounds(-1, 10, 400, 100));
        Assert.True(ScreenshotCoordinateMapper.IsInsideCapturedBounds(399, 99, 400, 100));
    }

    [Fact]
    public void WithFallbackMetadata_MarksActiveWindowFallbackToFullDesktop()
    {
        var fullDesktop = new ScreenshotCaptureResult
        {
            FilePath = "step-001.png",
            CaptureMode = ScreenshotCaptureMode.FullVirtualDesktop,
            CaptureLeft = 0,
            CaptureTop = 0,
            CaptureRight = 1920,
            CaptureBottom = 1080,
            LocalClickX = 25,
            LocalClickY = 30,
            ScreenshotWidth = 1920,
            ScreenshotHeight = 1080,
            RequestedScreenshotMode = ScreenshotMode.FullDesktop,
            ActualScreenshotMode = ScreenshotMode.FullDesktop,
            CapturedBounds = new ScreenBounds(0, 0, 1920, 1080),
            GlobalClickX = 25,
            GlobalClickY = 30,
            HighlightX = 25,
            HighlightY = 30,
            HighlightWasInsideCapturedBounds = true,
            BoundsSource = "VirtualScreen"
        };

        var fallback = ScreenshotFallbackFactory.WithFallbackMetadata(fullDesktop, ScreenshotMode.ActiveWindow, "active window failed");

        Assert.Equal(ScreenshotMode.ActiveWindow, fallback.RequestedScreenshotMode);
        Assert.Equal(ScreenshotMode.FullDesktop, fallback.ActualScreenshotMode);
        Assert.True(fallback.UsedScreenshotFallback);
        Assert.Equal("active window failed", fallback.ScreenshotError);
        Assert.Equal("VirtualScreen", fallback.BoundsSource);
        Assert.True(fallback.HighlightWasInsideCapturedBounds);
    }
}
