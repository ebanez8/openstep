namespace OpenSteps.Capture;

public sealed record DisplayMonitorInfo(
    IntPtr MonitorHandle,
    string? DeviceName,
    int BoundsLeft,
    int BoundsTop,
    int BoundsRight,
    int BoundsBottom,
    int WorkAreaLeft,
    int WorkAreaTop,
    int WorkAreaRight,
    int WorkAreaBottom,
    bool IsPrimary,
    int Index,
    uint? DpiX,
    uint? DpiY)
{
    public int Width => BoundsRight - BoundsLeft;

    public int Height => BoundsBottom - BoundsTop;
}
