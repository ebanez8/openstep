namespace OpenSteps.Capture;

public sealed class KeyboardInputEventArgs(KeyboardInputKind kind, string? keyName, string? shortcutName) : EventArgs
{
    public KeyboardInputKind Kind { get; } = kind;

    public string? KeyName { get; } = keyName;

    public string? ShortcutName { get; } = shortcutName;
}
