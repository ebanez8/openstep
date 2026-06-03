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
    private readonly WindowBoundsService _windowBoundsService;

    public ScreenshotService(MonitorService? monitorService = null, WindowBoundsService? windowBoundsService = null)
    {
        _monitorService = monitorService ?? new MonitorService();
        _windowBoundsService = windowBoundsService ?? new WindowBoundsService();
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
        ScreenshotMode requestedMode,
        bool drawHighlight = true,
        CancellationToken cancellationToken = default)
    {
        return CaptureAsync(sessionDirectory, $"step-{stepIndex:000}.png", clickX, clickY, requestedMode, drawHighlight, cancellationToken);
    }

    public Task<ScreenshotCaptureResult> CaptureAsync(
        string sessionDirectory,
        string fileName,
        int clickX,
        int clickY,
        ScreenshotMode requestedMode,
        bool drawHighlight = true,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(sessionDirectory);

            if (requestedMode == ScreenshotMode.ActiveWindow)
            {
                try
                {
                    return CaptureActiveWindow(sessionDirectory, fileName, clickX, clickY, drawHighlight, cancellationToken);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Runtime.InteropServices.ExternalException or ArgumentException)
                {
                    var fullDesktop = CaptureFullDesktop(sessionDirectory, fileName, clickX, clickY, drawHighlight, cancellationToken);
                    return ScreenshotFallbackFactory.WithFallbackMetadata(fullDesktop, requestedMode, ex.Message);
                }
            }

            return CaptureFullDesktop(sessionDirectory, fileName, clickX, clickY, drawHighlight, cancellationToken);
        }, cancellationToken);
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
            return CaptureLegacy(sessionDirectory, fileName, clickX, clickY, captureMode, drawHighlight, cancellationToken);
        }, cancellationToken);
    }

    private ScreenshotCaptureResult CaptureActiveWindow(
        string sessionDirectory,
        string fileName,
        int clickX,
        int clickY,
        bool drawHighlight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bounds = _windowBoundsService.GetForegroundWindowBounds();
        var captureBounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        var (highlightX, highlightY) = ScreenshotCoordinateMapper.ToCapturedPoint(clickX, clickY, bounds.Left, bounds.Top);
        var highlightInside = ScreenshotCoordinateMapper.IsInsideCapturedBounds(highlightX, highlightY, bounds.Width, bounds.Height);
        var filePath = Path.Combine(sessionDirectory, fileName);

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, captureBounds.Size, CopyPixelOperation.SourceCopy);
            if (drawHighlight && highlightInside)
            {
                DrawClickHighlight(graphics, highlightX, highlightY);
            }
        }

        bitmap.Save(filePath, ImageFormat.Png);

        return new ScreenshotCaptureResult
        {
            FilePath = filePath,
            CaptureMode = ScreenshotCaptureMode.FullVirtualDesktop,
            Monitor = _monitorService.GetMonitorFromPoint(clickX, clickY),
            CaptureLeft = bounds.Left,
            CaptureTop = bounds.Top,
            CaptureRight = bounds.Right,
            CaptureBottom = bounds.Bottom,
            LocalClickX = highlightX,
            LocalClickY = highlightY,
            ScreenshotWidth = bitmap.Width,
            ScreenshotHeight = bitmap.Height,
            RequestedScreenshotMode = ScreenshotMode.ActiveWindow,
            ActualScreenshotMode = ScreenshotMode.ActiveWindow,
            UsedScreenshotFallback = false,
            CapturedBounds = new ScreenBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
            GlobalClickX = clickX,
            GlobalClickY = clickY,
            HighlightX = highlightX,
            HighlightY = highlightY,
            HighlightWasInsideCapturedBounds = highlightInside,
            BoundsSource = bounds.Source
        };
    }

    private ScreenshotCaptureResult CaptureFullDesktop(
        string sessionDirectory,
        string fileName,
        int clickX,
        int clickY,
        bool drawHighlight,
        CancellationToken cancellationToken)
    {
        return CaptureLegacy(sessionDirectory, fileName, clickX, clickY, ScreenshotCaptureMode.FullVirtualDesktop, drawHighlight, cancellationToken);
    }

    private ScreenshotCaptureResult CaptureLegacy(
        string sessionDirectory,
        string fileName,
        int clickX,
        int clickY,
        ScreenshotCaptureMode captureMode,
        bool drawHighlight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Mouse hooks, Win32 monitor bounds, and Graphics.CopyFromScreen all use physical
        // virtual-screen pixels. WPF UI coordinates must be converted before reaching here.
        var monitor = _monitorService.GetMonitorFromPoint(clickX, clickY);
        var bounds = GetCaptureBounds(captureMode, monitor);
        var (localClickX, localClickY) = MonitorCoordinateMapper.ToLocalPoint(clickX, clickY, bounds.Left, bounds.Top);
        var highlightInside = ScreenshotCoordinateMapper.IsInsideCapturedBounds(localClickX, localClickY, bounds.Width, bounds.Height);
        var filePath = Path.Combine(sessionDirectory, fileName);

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            if (drawHighlight && highlightInside)
            {
                DrawClickHighlight(graphics, localClickX, localClickY);
            }
        }

        bitmap.Save(filePath, ImageFormat.Png);
        return new ScreenshotCaptureResult
        {
            FilePath = filePath,
            CaptureMode = captureMode,
            Monitor = monitor,
            CaptureLeft = bounds.Left,
            CaptureTop = bounds.Top,
            CaptureRight = bounds.Right,
            CaptureBottom = bounds.Bottom,
            LocalClickX = localClickX,
            LocalClickY = localClickY,
            ScreenshotWidth = bitmap.Width,
            ScreenshotHeight = bitmap.Height,
            RequestedScreenshotMode = ScreenshotMode.FullDesktop,
            ActualScreenshotMode = ScreenshotMode.FullDesktop,
            UsedScreenshotFallback = false,
            CapturedBounds = new ScreenBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
            GlobalClickX = clickX,
            GlobalClickY = clickY,
            HighlightX = localClickX,
            HighlightY = localClickY,
            HighlightWasInsideCapturedBounds = highlightInside,
            BoundsSource = captureMode == ScreenshotCaptureMode.FullVirtualDesktop ? "VirtualScreen" : "MonitorContainingClick"
        };
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
