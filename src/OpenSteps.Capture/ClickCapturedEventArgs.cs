namespace OpenSteps.Capture;

public sealed class ClickCapturedEventArgs(int x, int y) : EventArgs
{
    public int X { get; } = x;

    public int Y { get; } = y;
}
