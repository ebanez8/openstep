using System.Runtime.InteropServices;

namespace OpenSteps.Capture;

public sealed class GlobalKeyboardHook : IDisposable
{
    private readonly Func<bool> _shouldIgnoreKeyboard;
    private readonly NativeMethods.LowLevelHookProc _callback;
    private IntPtr _hookHandle;

    public GlobalKeyboardHook(Func<bool>? shouldIgnoreKeyboard = null)
    {
        _shouldIgnoreKeyboard = shouldIgnoreKeyboard ?? (() => false);
        _callback = HookCallback;
    }

    public event EventHandler<KeyboardInputEventArgs>? KeyboardInputCaptured;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to install the global keyboard hook.");
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
        if (nCode >= 0
            && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN)
            && !_shouldIgnoreKeyboard())
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var captured = ClassifyKey((int)data.VkCode);
                if (captured is not null)
                {
                    KeyboardInputCaptured?.Invoke(this, captured);
                }
            }
            catch
            {
                // Keyboard capture should never interrupt the target application or the hook chain.
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static KeyboardInputEventArgs? ClassifyKey(int vkCode)
    {
        var ctrl = IsDown(0x11) || IsDown(0xA2) || IsDown(0xA3);
        var alt = IsDown(0x12) || IsDown(0xA4) || IsDown(0xA5);
        var shift = IsDown(0x10) || IsDown(0xA0) || IsDown(0xA1);

        if (ctrl)
        {
            var shortcut = vkCode switch
            {
                0x41 => "Ctrl+A",
                0x43 => "Ctrl+C",
                0x53 => "Ctrl+S",
                0x56 => "Ctrl+V",
                0x5A => "Ctrl+Z",
                _ => null
            };

            if (shortcut is not null)
            {
                return new KeyboardInputEventArgs(KeyboardInputKind.Shortcut, null, shortcut);
            }
        }

        var special = vkCode switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            0x2E => "Delete",
            0x25 => "Left Arrow",
            0x26 => "Up Arrow",
            0x27 => "Right Arrow",
            0x28 => "Down Arrow",
            _ => null
        };

        if (special is not null)
        {
            return new KeyboardInputEventArgs(KeyboardInputKind.SpecialKey, special, null);
        }

        if (alt || ctrl)
        {
            return null;
        }

        if (IsPrintableTypingKey(vkCode) || vkCode == 0x20)
        {
            return new KeyboardInputEventArgs(KeyboardInputKind.Text, shift ? "Shift+Text" : "Text", null);
        }

        return null;
    }

    private static bool IsPrintableTypingKey(int vkCode)
    {
        return vkCode is >= 0x30 and <= 0x39
            or >= 0x41 and <= 0x5A
            or >= 0x60 and <= 0x6F
            or >= 0xBA and <= 0xC0
            or >= 0xDB and <= 0xDE;
    }

    private static bool IsDown(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }
}
