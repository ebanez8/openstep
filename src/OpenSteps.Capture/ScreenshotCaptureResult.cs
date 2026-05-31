using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed record ScreenshotCaptureResult(
    string FilePath,
    ScreenshotCaptureMode CaptureMode,
    DisplayMonitorInfo Monitor,
    int CaptureLeft,
    int CaptureTop,
    int CaptureRight,
    int CaptureBottom,
    int LocalClickX,
    int LocalClickY,
    int ScreenshotWidth,
    int ScreenshotHeight);
