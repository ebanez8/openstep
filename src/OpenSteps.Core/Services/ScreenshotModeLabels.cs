using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public static class ScreenshotModeLabels
{
    public static string GetDisplayText(ScreenshotMode mode)
    {
        return mode == ScreenshotMode.ActiveWindow ? "Active Window" : "Full Desktop";
    }
}
