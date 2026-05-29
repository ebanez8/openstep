using System.Diagnostics;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class ActiveWindowService
{
    public ActiveWindowInfo Capture()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return new ActiveWindowInfo(IntPtr.Zero, null, null, null, null);
        }

        var title = GetTitle(handle);
        string? processName = null;
        string? executablePath = null;

        try
        {
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;

            try
            {
                executablePath = process.MainModule?.FileName;
            }
            catch
            {
                executablePath = null;
            }
        }
        catch
        {
            processName = null;
        }

        ScreenBounds? bounds = null;
        if (NativeMethods.GetWindowRect(handle, out var rect))
        {
            bounds = new ScreenBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        return new ActiveWindowInfo(handle, title, processName, executablePath, bounds);
    }

    private static string? GetTitle(IntPtr handle)
    {
        Span<char> buffer = stackalloc char[512];
        var length = NativeMethods.GetWindowText(handle, buffer, buffer.Length);
        return length <= 0 ? null : new string(buffer[..length]);
    }
}
