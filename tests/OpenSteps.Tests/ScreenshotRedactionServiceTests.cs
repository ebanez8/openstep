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

    [Fact]
    public void ApplyRedactions_RedCircleDrawsRedPixels()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "redacted.png");
        CreateGradientImage(original);

        new ScreenshotRedactionService().ApplyRedactions(original, output, [new RedactionRegion(4, 4, 30, 30, RedactionMode.RedCircle)]);

        using var after = new Bitmap(output);
        var redPixels = 0;
        for (var y = 0; y < after.Height; y++)
        {
            for (var x = 0; x < after.Width; x++)
            {
                var pixel = after.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G < 80 && pixel.B < 80)
                {
                    redPixels++;
                }
            }
        }

        Assert.True(redPixels > 0);
    }

    [Fact]
    public void ApplyEdits_DrawsRectangleAnnotation()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "annotated.png");
        CreateSolidImage(original, Color.White, 40, 40);

        new ScreenshotRedactionService().ApplyEdits(
            original,
            output,
            [],
            [new ScreenshotAnnotation
            {
                Type = ScreenshotAnnotationType.Rectangle,
                X1 = 5,
                Y1 = 5,
                X2 = 25,
                Y2 = 25,
                Color = "#D92D20",
                StrokeThickness = 3
            }]);

        using var after = new Bitmap(output);
        Assert.True(HasRedPixel(after));
        Assert.Equal(Color.White.ToArgb(), after.GetPixel(30, 30).ToArgb());
    }

    [Fact]
    public void ApplyEdits_DrawsHighlightAnnotation()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "annotated.png");
        CreateSolidImage(original, Color.White, 40, 40);

        new ScreenshotRedactionService().ApplyEdits(
            original,
            output,
            [],
            [new ScreenshotAnnotation
            {
                Type = ScreenshotAnnotationType.Highlight,
                X1 = 5,
                Y1 = 5,
                X2 = 25,
                Y2 = 25,
                Color = "#FFD84D",
                Opacity = 0.35,
                StrokeThickness = 1
            }]);

        using var after = new Bitmap(output);
        Assert.NotEqual(Color.White.ToArgb(), after.GetPixel(10, 10).ToArgb());
    }

    [Fact]
    public void ApplyEdits_DrawsArrowAndMarkerAnnotations()
    {
        var temp = CreateTempDirectory();
        var original = Path.Combine(temp, "original.png");
        var output = Path.Combine(temp, "annotated.png");
        CreateSolidImage(original, Color.White, 80, 80);

        new ScreenshotRedactionService().ApplyEdits(
            original,
            output,
            [],
            [
                new ScreenshotAnnotation
                {
                    Type = ScreenshotAnnotationType.Arrow,
                    X1 = 5,
                    Y1 = 5,
                    X2 = 60,
                    Y2 = 60,
                    Color = "#D92D20",
                    StrokeThickness = 4
                },
                new ScreenshotAnnotation
                {
                    Type = ScreenshotAnnotationType.Marker,
                    X1 = 40,
                    Y1 = 20,
                    X2 = 12,
                    Text = "1",
                    Color = "#D92D20",
                    StrokeThickness = 3
                }
            ]);

        using var after = new Bitmap(output);
        Assert.True(HasRedPixel(after));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenSteps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateSolidImage(string path, Color color, int width = 10, int height = 10)
    {
        using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(color);
        }

        image.Save(path, ImageFormat.Png);
    }

    private static bool HasRedPixel(Bitmap image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.R > 180 && pixel.G < 120 && pixel.B < 120)
                {
                    return true;
                }
            }
        }

        return false;
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
