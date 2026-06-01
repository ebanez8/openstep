using System.Drawing;
using System.Drawing.Imaging;
using OpenSteps.Capture;
using OpenSteps.Core.Models;

namespace OpenSteps.Tests;

public sealed class ScreenshotRedactionServiceTests
{
    [Fact]
    public void ApplyRedactions_CreatesOutputFile()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateSolidImage(original, Color.White);

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(2, 2, 4, 4, RedactionMode.BlackBox)]);

        Assert.True(File.Exists(output));
    }

    [Fact]
    public void ApplyRedactions_BlackBoxChangesPixelsInsideSelectedRectangle()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateSolidImage(original, Color.White);

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(2, 2, 4, 4, RedactionMode.BlackBox)]);

        using var image = new Bitmap(output);
        Assert.Equal(Color.Black.ToArgb(), image.GetPixel(3, 3).ToArgb());
    }

    [Fact]
    public void ApplyRedactions_BlackBoxDoesNotChangePixelsOutsideSelectedRectangle()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateSolidImage(original, Color.White);

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(2, 2, 4, 4, RedactionMode.BlackBox)]);

        using var image = new Bitmap(output);
        Assert.Equal(Color.White.ToArgb(), image.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void ApplyRedactions_PixelateChangesPixelsInsideSelectedRectangle()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateGradientImage(original);

        using var before = new Bitmap(original);
        var beforePixel = before.GetPixel(7, 7).ToArgb();

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(0, 0, 28, 28, RedactionMode.Pixelate)]);

        using var after = new Bitmap(output);
        Assert.NotEqual(beforePixel, after.GetPixel(7, 7).ToArgb());
    }

    [Fact]
    public void ApplyRedactions_PixelateDoesNotChangePixelsOutsideSelectedRectangle()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateGradientImage(original);

        using var before = new Bitmap(original);
        var beforePixel = before.GetPixel(35, 35).ToArgb();

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(0, 0, 28, 28, RedactionMode.Pixelate)]);

        using var after = new Bitmap(output);
        Assert.Equal(beforePixel, after.GetPixel(35, 35).ToArgb());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateSolidImage(string path, Color color)
    {
        using var image = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(color);
        }

        image.Save(path, ImageFormat.Png);
    }

    private static void CreateGradientImage(string path)
    {
        using var image = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image.SetPixel(x, y, Color.FromArgb(255, x * 5, y * 5, (x + y) * 3));
            }
        }

        image.Save(path, ImageFormat.Png);
    }
}
