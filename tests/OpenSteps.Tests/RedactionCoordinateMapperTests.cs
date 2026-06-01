using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class RedactionCoordinateMapperTests
{
    [Fact]
    public void DisplayToImagePoint_MapsUniformDisplayCoordinatesToImagePixels()
    {
        var point = RedactionCoordinateMapper.DisplayToImagePoint(
            mouseX: 200,
            mouseY: 100,
            containerWidth: 400,
            containerHeight: 200,
            imageWidth: 100,
            imageHeight: 100);

        Assert.NotNull(point);
        Assert.Equal(50, point.Value.X, precision: 3);
        Assert.Equal(50, point.Value.Y, precision: 3);
    }

    [Fact]
    public void ToImageRegion_ClampsToImageBounds()
    {
        var region = RedactionCoordinateMapper.ToImageRegion(
            startX: 200,
            startY: 100,
            endX: 500,
            endY: 300,
            containerWidth: 400,
            containerHeight: 200,
            imageWidth: 100,
            imageHeight: 100,
            mode: RedactionMode.Pixelate);

        Assert.NotNull(region);
        Assert.Equal(50, region.X);
        Assert.Equal(50, region.Y);
        Assert.Equal(50, region.Width);
        Assert.Equal(50, region.Height);
    }
}
