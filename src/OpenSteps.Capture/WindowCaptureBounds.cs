namespace OpenSteps.Capture;

public sealed class WindowCaptureBounds
{
    public IntPtr Hwnd { get; set; }

    public int Left { get; set; }

    public int Top { get; set; }

    public int Right { get; set; }

    public int Bottom { get; set; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public string Source { get; set; } = string.Empty;
}
