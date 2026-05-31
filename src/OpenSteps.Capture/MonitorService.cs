using System.Runtime.InteropServices;

namespace OpenSteps.Capture;

public sealed class MonitorService
{
    public DisplayMonitorInfo GetMonitorFromPoint(int screenX, int screenY)
    {
        var point = new NativeMethods.POINT { X = screenX, Y = screenY };
        var handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to find a monitor for the screen point.");
        }

        var all = GetAllMonitors();
        return all.FirstOrDefault(monitor => monitor.MonitorHandle == handle)
            ?? ReadMonitor(handle, all.Count + 1);
    }

    public IReadOnlyList<DisplayMonitorInfo> GetAllMonitors()
    {
        var monitors = new List<DisplayMonitorInfo>();
        NativeMethods.MonitorEnumProc callback = EnumerateMonitor;

        bool EnumerateMonitor(IntPtr handle, IntPtr hdcMonitor, ref NativeMethods.RECT monitorRect, IntPtr data)
        {
            monitors.Add(ReadMonitor(handle, monitors.Count + 1));
            return true;
        }

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new InvalidOperationException("Unable to enumerate display monitors.");
        }

        return monitors;
    }

    private static DisplayMonitorInfo ReadMonitor(IntPtr handle, int index)
    {
        var info = new NativeMethods.MONITORINFOEX
        {
            Size = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            DeviceName = string.Empty
        };

        if (!NativeMethods.GetMonitorInfo(handle, ref info))
        {
            throw new InvalidOperationException("Unable to read monitor information.");
        }

        uint? dpiX = null;
        uint? dpiY = null;
        try
        {
            if (NativeMethods.GetDpiForMonitor(handle, NativeMethods.MDT_EFFECTIVE_DPI, out var x, out var y) == 0)
            {
                dpiX = x;
                dpiY = y;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return new DisplayMonitorInfo(
            handle,
            string.IsNullOrWhiteSpace(info.DeviceName) ? null : info.DeviceName,
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right,
            info.Monitor.Bottom,
            info.Work.Left,
            info.Work.Top,
            info.Work.Right,
            info.Work.Bottom,
            (info.Flags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
            index,
            dpiX,
            dpiY);
    }
}
