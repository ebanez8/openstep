using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class MonitorCoordinateMapperTests
{
    [Theory]
    [InlineData(1920, 0, 3840, 1080, 2500, 300, 580, 300)]
    [InlineData(-1920, 0, 0, 1080, -1200, 300, 720, 300)]
    [InlineData(0, -1080, 1920, 0, 500, -500, 500, 580)]
    [InlineData(0, 0, 2560, 1440, 100, 200, 100, 200)]
    public void ToLocalPoint_ConvertsAbsoluteClickToMonitorLocalPoint(
        int left,
        int top,
        int right,
        int bottom,
        int clickX,
        int clickY,
        int expectedX,
        int expectedY)
    {
        _ = right;
        _ = bottom;

        var local = MonitorCoordinateMapper.ToLocalPoint(clickX, clickY, left, top);

        Assert.Equal(expectedX, local.X);
        Assert.Equal(expectedY, local.Y);
    }
}
