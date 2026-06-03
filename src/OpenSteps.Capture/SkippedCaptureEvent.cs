namespace OpenSteps.Capture;

public sealed class SkippedCaptureEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public int X { get; set; }

    public int Y { get; set; }

    public string Reason { get; set; } = string.Empty;

    public IntPtr HitHwnd { get; set; }

    public IntPtr RootHwnd { get; set; }

    public string? HitClassName { get; set; }

    public string? RootClassName { get; set; }

    public string? ProcessName { get; set; }

    public IntPtr? ForegroundHwndBeforeDelay { get; set; }

    public IntPtr? ForegroundHwndAfterDelay { get; set; }

    public string DisplaySummary =>
        $"{Timestamp:HH:mm:ss} - {Reason} at ({X}, {Y}); hit={FormatHandle(HitHwnd)} {HitClassName ?? "(unknown)"}, root={FormatHandle(RootHwnd)} {RootClassName ?? "(unknown)"}, process={ProcessName ?? "(unknown)"}, foreground before={FormatHandle(ForegroundHwndBeforeDelay)}, after={FormatHandle(ForegroundHwndAfterDelay)}";

    private static string FormatHandle(IntPtr? hwnd)
    {
        return hwnd.HasValue ? $"0x{hwnd.Value.ToInt64():X}" : "(unknown)";
    }
}
