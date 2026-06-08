using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class ScreenshotRedactionService
{
    public void ApplyRedactions(string originalImagePath, string outputImagePath, IReadOnlyList<RedactionRegion> regions)
    {
        ApplyEdits(originalImagePath, outputImagePath, regions, []);
    }

    public void ApplyEdits(
        string originalImagePath,
        string outputImagePath,
        IReadOnlyList<RedactionRegion> regions,
        IReadOnlyList<ScreenshotAnnotation> annotations)
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
                else if (region.Mode == RedactionMode.RedCircle)
                {
                    var rectangle = Clamp(region, source.Width, source.Height);
                    if (rectangle.Width > 0 && rectangle.Height > 0)
                    {
                        using var pen = new Pen(Color.Red, Math.Max(4, Math.Min(rectangle.Width, rectangle.Height) / 12f));
                        graphics.DrawEllipse(pen, rectangle);
                    }
                }
            }

            foreach (var annotation in annotations)
            {
                DrawAnnotation(graphics, source.Width, source.Height, annotation);
            }
        }

        redacted.Save(outputImagePath, ImageFormat.Png);
    }

    private static void DrawAnnotation(Graphics graphics, int imageWidth, int imageHeight, ScreenshotAnnotation annotation)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var color = ParseColor(annotation.Color, Color.Red);
        var width = Math.Max(1f, (float)annotation.StrokeThickness);
        var opacity = Math.Clamp(annotation.Opacity, 0, 1);

        if (annotation.Type == ScreenshotAnnotationType.Rectangle)
        {
            var rectangle = Clamp(annotation, imageWidth, imageHeight);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            using var pen = new Pen(color, width);
            graphics.DrawRectangle(pen, rectangle);
        }
        else if (annotation.Type == ScreenshotAnnotationType.Highlight)
        {
            var rectangle = Clamp(annotation, imageWidth, imageHeight);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            using var brush = new SolidBrush(Color.FromArgb((int)(255 * opacity), color));
            using var pen = new Pen(Color.FromArgb(190, color), Math.Max(1f, width / 2));
            graphics.FillRectangle(brush, rectangle);
            graphics.DrawRectangle(pen, rectangle);
        }
        else if (annotation.Type == ScreenshotAnnotationType.Arrow)
        {
            using var pen = new Pen(color, width)
            {
                CustomEndCap = new AdjustableArrowCap(width * 1.6f, width * 2.2f)
            };
            graphics.DrawLine(
                pen,
                ClampCoordinate(annotation.X1, imageWidth),
                ClampCoordinate(annotation.Y1, imageHeight),
                ClampCoordinate(annotation.X2, imageWidth),
                ClampCoordinate(annotation.Y2, imageHeight));
        }
        else if (annotation.Type == ScreenshotAnnotationType.Marker)
        {
            DrawMarker(graphics, imageWidth, imageHeight, annotation, color);
        }
    }

    private static void DrawMarker(Graphics graphics, int imageWidth, int imageHeight, ScreenshotAnnotation annotation, Color color)
    {
        var radius = Math.Max(12, Math.Abs(annotation.X2));
        var centerX = ClampCoordinate(annotation.X1, imageWidth);
        var centerY = ClampCoordinate(annotation.Y1, imageHeight);
        var rectangle = new RectangleF((float)(centerX - radius), (float)(centerY - radius), (float)(radius * 2), (float)(radius * 2));

        using var fill = new SolidBrush(color);
        using var border = new Pen(Color.White, Math.Max(2f, (float)annotation.StrokeThickness / 2));
        graphics.FillEllipse(fill, rectangle);
        graphics.DrawEllipse(border, rectangle);

        var text = string.IsNullOrWhiteSpace(annotation.Text) ? "1" : annotation.Text.Trim();
        using var font = new Font("Segoe UI", Math.Max(10f, (float)radius * 0.95f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, textBrush, rectangle, format);
    }

    private static Rectangle Clamp(RedactionRegion region, int imageWidth, int imageHeight)
    {
        var left = Math.Clamp(region.X, 0, imageWidth);
        var top = Math.Clamp(region.Y, 0, imageHeight);
        var right = Math.Clamp(region.X + region.Width, 0, imageWidth);
        var bottom = Math.Clamp(region.Y + region.Height, 0, imageHeight);
        return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
    }

    private static Rectangle Clamp(ScreenshotAnnotation annotation, int imageWidth, int imageHeight)
    {
        var left = Math.Clamp((int)Math.Round(Math.Min(annotation.X1, annotation.X2)), 0, imageWidth);
        var top = Math.Clamp((int)Math.Round(Math.Min(annotation.Y1, annotation.Y2)), 0, imageHeight);
        var right = Math.Clamp((int)Math.Round(Math.Max(annotation.X1, annotation.X2)), 0, imageWidth);
        var bottom = Math.Clamp((int)Math.Round(Math.Max(annotation.Y1, annotation.Y2)), 0, imageHeight);
        return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
    }

    private static float ClampCoordinate(double value, int max)
    {
        return (float)Math.Clamp(value, 0, max);
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch
        {
            return fallback;
        }
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
