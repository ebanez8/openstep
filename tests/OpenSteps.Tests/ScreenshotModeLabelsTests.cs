using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class ScreenshotModeLabelsTests
{
    [Theory]
    [InlineData(ScreenshotMode.FullDesktop, "Full Desktop")]
    [InlineData(ScreenshotMode.ActiveWindow, "Active Window")]
    public void GetDisplayText_ReturnsReadableModeName(ScreenshotMode mode, string expected)
    {
        Assert.Equal(expected, ScreenshotModeLabels.GetDisplayText(mode));
    }
}
