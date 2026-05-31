using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Capture;

public sealed class ScreenshotService
{
    private readonly MonitorService _monitorService;

    public ScreenshotService(MonitorService? monitorService = null)
    {
        _monitorService = monitorService ?? new MonitorService();
    }

    public Task<string> CaptureVirtualDesktopAsync(string sessionDirectory, int stepIndex, int clickX, int clickY, CancellationToken cancellationToken = default)
    {
        return CaptureVirtualDesktopAsync(sessionDirectory, $"step-{stepIndex:000}.png", clickX, clickY, drawHighlight: true, cancellationToken);
    }

    public async Task<string> CaptureVirtualDesktopAsync(string sessionDirectory, string fileName, int clickX, int clickY, bool drawHighlight = true, CancellationToken cancellationToken = default)
    {
        var result = await CaptureAsync(sessionDirectory, fileName, clickX, clickY, ScreenshotCaptureMode.FullVirtualDesktop, drawHighlight, cancellationToken);
        return result.FilePath;
    }

    public Task<ScreenshotCaptureResult> CaptureAsync(
        string sessionDirectory,
        int stepIndex,
        int clickX,
        int clickY,
        ScreenshotCaptureMode captureMode = ScreenshotCaptureMode.MonitorContainingClick,
        bool drawHighlight = true,
        CancellationToken cancellationToken = default)
    {
        return CaptureAsync(sessionDirectory, $"step-{stepIndex:000}.png", clickX, clickY, captureMode, drawHighlight, cancellationToken);
    }

    public Task<ScreenshotCaptureResult> CaptureAsync(
        string sessionDirectory,
        string fileName,
        int clickX,
        int clickY,
        ScreenshotCaptureMode captureMode,
        bool drawHighlight = true,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(sessionDirectory);

            // Mouse hooks, Win32 monitor bounds, and Graphics.CopyFromScreen all use physical
            // virtual-screen pixels. WPF UI coordinates must be converted before reaching here.
            var monitor = _monitorService.GetMonitorFromPoint(clickX, clickY);
            var bounds = GetCaptureBounds(captureMode, monitor);
            var (localClickX, localClickY) = MonitorCoordinateMapper.ToLocalPoint(clickX, clickY, bounds.Left, bounds.Top);
            var filePath = Path.Combine(sessionDirectory, fileName);

            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                if (drawHighlight)
                {
                    DrawClickHighlight(graphics, localClickX, localClickY);
                }
            }

            bitmap.Save(filePath, ImageFormat.Png);
            return new ScreenshotCaptureResult(
                filePath,
                captureMode,
                monitor,
                bounds.Left,
                bounds.Top,
                bounds.Right,
                bounds.Bottom,
                localClickX,
                localClickY,
                bitmap.Width,
                bitmap.Height);
        }, cancellationToken);
    }

    private static Rectangle GetCaptureBounds(ScreenshotCaptureMode captureMode, DisplayMonitorInfo monitor)
    {
        if (captureMode == ScreenshotCaptureMode.FullVirtualDesktop)
        {
            return SystemInformation.VirtualScreen;
        }

        return Rectangle.FromLTRB(monitor.BoundsLeft, monitor.BoundsTop, monitor.BoundsRight, monitor.BoundsBottom);
    }

    private static void DrawClickHighlight(Graphics graphics, int x, int y)
    {
        const int radius = 22;
        using var pen = new Pen(Color.FromArgb(230, 220, 30, 30), 5);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
    }
}
