using System.Runtime.InteropServices;

namespace OpenSteps.Capture;

public sealed class GlobalMouseHook : IDisposable
{
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(250);
    private readonly Func<int, int, bool> _shouldIgnoreClick;
    private readonly NativeMethods.LowLevelHookProc _callback;
    private DateTimeOffset _lastClickAt = DateTimeOffset.MinValue;
    private IntPtr _hookHandle;

    public GlobalMouseHook(Func<int, int, bool>? shouldIgnoreClick = null)
    {
        _shouldIgnoreClick = shouldIgnoreClick ?? ((_, _) => false);
        _callback = HookCallback;
    }

    public event EventHandler<ClickCapturedEventArgs>? ClickCaptured;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _callback, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to install the global mouse hook.");
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == NativeMethods.WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var now = DateTimeOffset.Now;
            if (now - _lastClickAt >= _debounce && !_shouldIgnoreClick(data.Pt.X, data.Pt.Y))
            {
                _lastClickAt = now;
                ClickCaptured?.Invoke(this, new ClickCapturedEventArgs(data.Pt.X, data.Pt.Y));
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
