using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenSteps.Capture;

public sealed class WindowBoundsService
{
    public WindowCaptureBounds GetForegroundWindowBounds()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Foreground window handle was empty.");
        }

        return GetWindowBounds(hwnd);
    }

    public WindowCaptureBounds GetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle was empty.");
        }

        var bounds = TryGetDwmExtendedFrameBounds(hwnd) ?? TryGetWindowRectBounds(hwnd);
        if (bounds is null)
        {
            throw new InvalidOperationException("Window bounds could not be read.");
        }

        Validate(bounds);
        return bounds;
    }

    private static WindowCaptureBounds? TryGetDwmExtendedFrameBounds(IntPtr hwnd)
    {
        var result = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out var rect,
            Marshal.SizeOf<NativeMethods.RECT>());

        return result == 0
            ? FromRect(hwnd, rect, "DwmExtendedFrameBounds")
            : null;
    }

    private static WindowCaptureBounds? TryGetWindowRectBounds(IntPtr hwnd)
    {
        return NativeMethods.GetWindowRect(hwnd, out var rect)
            ? FromRect(hwnd, rect, "GetWindowRect")
            : null;
    }

    private static WindowCaptureBounds FromRect(IntPtr hwnd, NativeMethods.RECT rect, string source)
    {
        return new WindowCaptureBounds
        {
            Hwnd = hwnd,
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom,
            Source = source
        };
    }

    private static void Validate(WindowCaptureBounds bounds)
    {
        if (bounds.Hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle was empty.");
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException($"Window bounds were invalid: {bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}.");
        }

        var virtualScreen = SystemInformation.VirtualScreen;
        var maxWidth = Math.Max(virtualScreen.Width * 2, 20_000);
        var maxHeight = Math.Max(virtualScreen.Height * 2, 20_000);
        if (bounds.Width > maxWidth || bounds.Height > maxHeight)
        {
            throw new InvalidOperationException($"Window bounds were too large: {bounds.Width}x{bounds.Height}.");
        }

        var captureBounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        if (!captureBounds.IntersectsWith(virtualScreen))
        {
            throw new InvalidOperationException("Window bounds did not intersect the virtual screen.");
        }
    }
}
