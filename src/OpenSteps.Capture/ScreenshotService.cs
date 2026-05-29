using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace OpenSteps.Capture;

public sealed class ScreenshotService
{
    public Task<string> CaptureVirtualDesktopAsync(string sessionDirectory, int stepIndex, int clickX, int clickY, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(sessionDirectory);

            var bounds = SystemInformation.VirtualScreen;
            var filePath = Path.Combine(sessionDirectory, $"step-{stepIndex:000}.png");

            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                DrawClickHighlight(graphics, clickX - bounds.Left, clickY - bounds.Top);
            }

            bitmap.Save(filePath, ImageFormat.Png);
            return filePath;
        }, cancellationToken);
    }

    private static void DrawClickHighlight(Graphics graphics, int x, int y)
    {
        const int radius = 22;
        using var pen = new Pen(Color.FromArgb(230, 220, 30, 30), 5);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
    }
}
