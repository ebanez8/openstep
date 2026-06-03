using System.Diagnostics;
using System.Drawing;
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
        "MSTaskSwWClass",
        "ReBarWindow32",
        "ToolbarWindow32",
        "ClockButton",
        "TrayClockWClass",
        "TrayDummySearchControl",
        "Windows.UI.Input.InputSite.WindowClass",
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
        return CreateInfo(
            x,
            y,
            hit,
            root,
            GetClassName(hit),
            GetClassName(root),
            GetProcessName(root),
            openStepsWindowHandles,
            GetAncestorClassNames(hit),
            IsPointInTaskbarBounds(x, y));
    }

    public ClickTargetInfo ClassifyWindow(IntPtr hwnd, IReadOnlySet<IntPtr> openStepsWindowHandles)
    {
        var root = hwnd == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = hwnd;
        }

        return CreateInfo(0, 0, hwnd, root, GetClassName(hwnd), GetClassName(root), GetProcessName(root), openStepsWindowHandles, GetAncestorClassNames(hwnd));
    }

    public static ClickTargetInfo CreateInfo(
        int x,
        int y,
        IntPtr hitHwnd,
        IntPtr rootHwnd,
        string? hitClassName,
        string? rootClassName,
        string? processName,
        IReadOnlySet<IntPtr> openStepsWindowHandles,
        IReadOnlyList<string>? ancestorClassNames = null,
        bool pointInTaskbarBounds = false)
    {
        ancestorClassNames ??= [];
        var info = new ClickTargetInfo
        {
            X = x,
            Y = y,
            HitHwnd = hitHwnd,
            RootHwnd = rootHwnd,
            HitClassName = hitClassName,
            RootClassName = rootClassName,
            ProcessName = processName,
            AncestorClassNames = ancestorClassNames
        };

        if ((hitHwnd != IntPtr.Zero && openStepsWindowHandles.Contains(hitHwnd))
            || (rootHwnd != IntPtr.Zero && openStepsWindowHandles.Contains(rootHwnd)))
        {
            info.Classification = ClickClassification.OpenStepsWindow;
            info.SkipReason = "Skipped OpenSteps UI click";
            return info;
        }

        if (pointInTaskbarBounds || IsTaskbarOrShell(hitClassName, rootClassName, processName, ancestorClassNames))
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

    private static bool IsTaskbarOrShell(string? hitClassName, string? rootClassName, string? processName, IReadOnlyList<string> ancestorClassNames)
    {
        if (TaskbarClassNames.Contains(hitClassName ?? string.Empty)
            || TaskbarClassNames.Contains(rootClassName ?? string.Empty)
            || ancestorClassNames.Any(TaskbarClassNames.Contains))
        {
            return true;
        }

        var shellBridgeClass = string.Equals(hitClassName, "Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rootClassName, "Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hitClassName, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rootClassName, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase)
            || ancestorClassNames.Any(className =>
                string.Equals(className, "Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase));

        return shellBridgeClass && (ShellProcessNames.Contains(processName ?? string.Empty)
            || string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetAncestorClassNames(IntPtr hwnd)
    {
        var classNames = new List<string>();
        var seen = new HashSet<IntPtr>();
        var current = hwnd;
        while (current != IntPtr.Zero && seen.Add(current) && classNames.Count < 16)
        {
            var className = GetClassName(current);
            if (!string.IsNullOrWhiteSpace(className))
            {
                classNames.Add(className);
            }

            current = NativeMethods.GetParent(current);
        }

        return classNames;
    }

    private static bool IsPointInTaskbarBounds(int x, int y)
    {
        foreach (var hwnd in EnumerateTaskbarWindows())
        {
            if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                continue;
            }

            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IntPtr> EnumerateTaskbarWindows()
    {
        var primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            yield return primary;
        }

        var secondary = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (string.Equals(GetClassName(hwnd), "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                secondary.Add(hwnd);
            }

            return true;
        }, IntPtr.Zero);

        foreach (var hwnd in secondary)
        {
            yield return hwnd;
        }
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
