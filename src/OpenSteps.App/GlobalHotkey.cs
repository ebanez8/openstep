using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OpenSteps.Core.Models;

namespace OpenSteps.App;

/// <summary>A system-wide hotkey registered against a window's message loop.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4F53;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    private readonly Action _pressed;
    private bool _registered;

    public GlobalHotkey(Window window, Action pressed)
    {
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WndProc);
        _pressed = pressed;
    }

    /// <summary>Replaces any current registration. Returns false when the key is invalid or already taken.</summary>
    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        if (!TryGetVirtualKey(gesture, out var virtualKey))
        {
            return false;
        }

        _registered = RegisterHotKey(_handle, HotkeyId, (uint)gesture.Modifiers | ModNoRepeat, (uint)virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }

    public static bool TryGetVirtualKey(HotkeyGesture gesture, out int virtualKey)
    {
        virtualKey = 0;
        if (!Enum.TryParse<Key>(gesture.Key, ignoreCase: true, out var key) || !Enum.IsDefined(key) || key == Key.None)
        {
            return false;
        }

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _pressed();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
