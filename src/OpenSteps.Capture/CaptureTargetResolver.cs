namespace OpenSteps.Capture;

public sealed class CaptureTargetResolver
{
    public CaptureTargetResolution Resolve(ClickTargetInfo immediateTarget, ClickTargetInfo? foregroundAfterDelay)
    {
        if (immediateTarget.Classification == ClickClassification.OpenStepsWindow)
        {
            return Skip("SkippedOpenSteps", immediateTarget.SkipReason ?? "Skipped OpenSteps UI click");
        }

        if (immediateTarget.Classification == ClickClassification.TaskbarOrShell)
        {
            return Skip("SkippedTaskbar", immediateTarget.SkipReason ?? "Skipped taskbar/shell click");
        }

        if (immediateTarget.Classification == ClickClassification.RecordableAppWindow
            && immediateTarget.RootHwnd != IntPtr.Zero)
        {
            return new CaptureTargetResolution
            {
                TargetHwnd = immediateTarget.RootHwnd,
                ProcessName = immediateTarget.ProcessName,
                ResolutionSource = "ClickedWindowRoot",
                ShouldRecord = true
            };
        }

        if (foregroundAfterDelay?.Classification == ClickClassification.RecordableAppWindow
            && foregroundAfterDelay.RootHwnd != IntPtr.Zero)
        {
            return new CaptureTargetResolution
            {
                TargetHwnd = foregroundAfterDelay.RootHwnd,
                ProcessName = foregroundAfterDelay.ProcessName,
                ResolutionSource = "ForegroundAfterDelay",
                ShouldRecord = true
            };
        }

        if (foregroundAfterDelay?.Classification == ClickClassification.OpenStepsWindow)
        {
            return Skip("ForegroundAfterDelay", "Foreground remained OpenSteps after activation delay");
        }

        return Skip("Unknown", "No safe capture target could be resolved");
    }

    private static CaptureTargetResolution Skip(string source, string reason)
    {
        return new CaptureTargetResolution
        {
            ResolutionSource = source,
            ShouldRecord = false,
            SkipReason = reason
        };
    }
}
