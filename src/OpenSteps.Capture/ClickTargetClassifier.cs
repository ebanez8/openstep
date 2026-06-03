using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenSteps.Capture;

public sealed class ClickTargetClassifier
{
    private static readonly HashSet<string> TaskbarClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "TrayNotifyWnd",
        "MSTaskListWClass",
        "Start",
        "NotifyIconOverflowWindow"
    };

    private static readonly HashSet<string> DesktopClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW"
    };

    private static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost"
    };

    public ClickTargetInfo ClassifyPoint(int x, int y, IReadOnlySet<IntPtr> openStepsWindowHandles)
    {
        var hit = NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });
        var root = hit == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(hit, NativeMethods.GA_ROOT);
        return CreateInfo(x, y, hit, root, GetClassName(hit), GetClassName(root), GetProcessName(root), openStepsWindowHandles);
    }

    public ClickTargetInfo ClassifyWindow(IntPtr hwnd, IReadOnlySet<IntPtr> openStepsWindowHandles)
    {
        var root = hwnd == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = hwnd;
        }

        return CreateInfo(0, 0, hwnd, root, GetClassName(hwnd), GetClassName(root), GetProcessName(root), openStepsWindowHandles);
    }

    public static ClickTargetInfo CreateInfo(
        int x,
        int y,
        IntPtr hitHwnd,
        IntPtr rootHwnd,
        string? hitClassName,
        string? rootClassName,
        string? processName,
        IReadOnlySet<IntPtr> openStepsWindowHandles)
    {
        var info = new ClickTargetInfo
        {
            X = x,
            Y = y,
            HitHwnd = hitHwnd,
            RootHwnd = rootHwnd,
            HitClassName = hitClassName,
            RootClassName = rootClassName,
            ProcessName = processName
        };

        if ((hitHwnd != IntPtr.Zero && openStepsWindowHandles.Contains(hitHwnd))
            || (rootHwnd != IntPtr.Zero && openStepsWindowHandles.Contains(rootHwnd)))
        {
            info.Classification = ClickClassification.OpenStepsWindow;
            info.SkipReason = "Skipped OpenSteps UI click";
            return info;
        }

        if (IsTaskbarOrShell(hitClassName, rootClassName, processName))
        {
            info.Classification = ClickClassification.TaskbarOrShell;
            info.SkipReason = "Skipped taskbar/shell click";
            return info;
        }

        if (DesktopClassNames.Contains(hitClassName ?? string.Empty)
            || DesktopClassNames.Contains(rootClassName ?? string.Empty))
        {
            info.Classification = ClickClassification.DesktopShell;
            return info;
        }

        info.Classification = rootHwnd == IntPtr.Zero
            ? ClickClassification.Unknown
            : ClickClassification.RecordableAppWindow;
        return info;
    }

    private static bool IsTaskbarOrShell(string? hitClassName, string? rootClassName, string? processName)
    {
        if (TaskbarClassNames.Contains(hitClassName ?? string.Empty)
            || TaskbarClassNames.Contains(rootClassName ?? string.Empty))
        {
            return true;
        }

        var shellBridgeClass = string.Equals(hitClassName, "Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rootClassName, "Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hitClassName, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rootClassName, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase);

        return shellBridgeClass && ShellProcessNames.Contains(processName ?? string.Empty);
    }

    private static string? GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        Span<char> buffer = stackalloc char[256];
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return length <= 0 ? null : new string(buffer[..length]);
    }

    private static string? GetProcessName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
