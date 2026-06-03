namespace OpenSteps.Capture;

public sealed class CaptureTargetResolution
{
    public IntPtr TargetHwnd { get; set; }

    public string? WindowTitle { get; set; }

    public string? ProcessName { get; set; }

    public string ResolutionSource { get; set; } = "Unknown";

    public bool ShouldRecord { get; set; }

    public string? SkipReason { get; set; }
}
