namespace OpenSteps.Core.Models;

/// <summary>Values match the Win32 RegisterHotKey MOD_* flags.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8
}
