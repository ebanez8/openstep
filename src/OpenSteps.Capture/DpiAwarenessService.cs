namespace OpenSteps.Capture;

public sealed class DpiAwarenessService
{
    public string GetCurrentThreadAwareness()
    {
        try
        {
            var context = NativeMethods.GetThreadDpiAwarenessContext();
            var awareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(context);
            return awareness switch
            {
                NativeMethods.DPI_AWARENESS.DPI_AWARENESS_UNAWARE => "Unaware",
                NativeMethods.DPI_AWARENESS.DPI_AWARENESS_SYSTEM_AWARE => "SystemAware",
                NativeMethods.DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE => "PerMonitorAware",
                _ => awareness.ToString()
            };
        }
        catch
        {
            return "Unknown";
        }
    }
}
