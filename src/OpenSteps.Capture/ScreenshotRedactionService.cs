using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class ScreenshotRedactionService
{
    public void ApplyRedactions(string originalImagePath, string outputImagePath, IReadOnlyList<RedactionRegion> regions)
    {
        if (string.IsNullOrWhiteSpace(originalImagePath))
        {
            throw new ArgumentException("Original image path is required.", nameof(originalImagePath));
        }

        if (string.IsNullOrWhiteSpace(outputImagePath))
        {
            throw new ArgumentException("Output image path is required.", nameof(outputImagePath));
        }

        if (!File.Exists(originalImagePath))
        {
            throw new FileNotFoundException("Original screenshot was not found.", originalImagePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");

        using var source = new Bitmap(originalImagePath);
        using var redacted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(redacted))
        {
            graphics.DrawImageUnscaled(source, 0, 0);
            foreach (var region in regions)
            {
                if (region.Mode == RedactionMode.BlackBox)
                {
                    var rectangle = Clamp(region, source.Width, source.Height);
                    if (rectangle.Width > 0 && rectangle.Height > 0)
                    {
                        graphics.FillRectangle(Brushes.Black, rectangle);
                    }
                }
                else if (region.Mode == RedactionMode.Pixelate)
                {
                    Pixelate(redacted, region);
                }
            }
        }

        redacted.Save(outputImagePath, ImageFormat.Png);
    }

    private static Rectangle Clamp(RedactionRegion region, int imageWidth, int imageHeight)
    {
        var left = Math.Clamp(region.X, 0, imageWidth);
        var top = Math.Clamp(region.Y, 0, imageHeight);
        var right = Math.Clamp(region.X + region.Width, 0, imageWidth);
        var bottom = Math.Clamp(region.Y + region.Height, 0, imageHeight);
        return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
    }

    private static void Pixelate(Bitmap bitmap, RedactionRegion region)
    {
        var rectangle = Clamp(region, bitmap.Width, bitmap.Height);
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        const int blockSize = 18;
        for (var y = rectangle.Top; y < rectangle.Bottom; y += blockSize)
        {
            for (var x = rectangle.Left; x < rectangle.Right; x += blockSize)
            {
                FillPixelBlock(bitmap, x, y, Math.Min(rectangle.Right, x + blockSize), Math.Min(rectangle.Bottom, y + blockSize));
            }
        }
    }

    private static void FillPixelBlock(Bitmap bitmap, int left, int top, int right, int bottom)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var count = 0;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                red += pixel.R;
                green += pixel.G;
                blue += pixel.B;
                alpha += pixel.A;
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var average = Color.FromArgb((int)(alpha / count), (int)(red / count), (int)(green / count), (int)(blue / count));
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                bitmap.SetPixel(x, y, average);
            }
        }
    }
}
