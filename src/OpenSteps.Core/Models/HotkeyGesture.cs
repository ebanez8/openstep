namespace OpenSteps.Core.Models;

/// <summary>
/// A global hotkey such as "Ctrl+Alt+R". <see cref="Key"/> is a WPF <c>Key</c> name
/// (for example "R", "F9" or "D1"); digit keys are shown as plain digits.
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = new HotkeyGesture(HotkeyModifiers.None, string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Ctrl,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" or "windows" => HotkeyModifiers.Win,
                _ => (HotkeyModifiers?)null
            };

            if (modifier is null)
            {
                return false;
            }

            modifiers |= modifier.Value;
        }

        var key = parts[^1];
        if (key.Length == 1 && char.IsAsciiDigit(key[0]))
        {
            key = "D" + key;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        foreach (var (flag, name) in new[]
        {
            (HotkeyModifiers.Ctrl, "Ctrl"),
            (HotkeyModifiers.Alt, "Alt"),
            (HotkeyModifiers.Shift, "Shift"),
            (HotkeyModifiers.Win, "Win")
        })
        {
            if (Modifiers.HasFlag(flag))
            {
                parts.Add(name);
            }
        }

        parts.Add(Key.Length == 2 && Key[0] == 'D' && char.IsAsciiDigit(Key[1]) ? Key[1..] : Key);
        return string.Join("+", parts);
    }
}
