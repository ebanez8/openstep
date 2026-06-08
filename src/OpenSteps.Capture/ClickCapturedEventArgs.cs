using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class ClickCapturedEventArgs(
    int x,
    int y,
    StepActionType actionType = StepActionType.Click,
    string mouseButton = "Left",
    int clickCount = 1,
    string? doubleClickDetectionReason = null) : EventArgs
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public StepActionType ActionType { get; } = actionType;

    public string MouseButton { get; } = mouseButton;

    public int ClickCount { get; } = clickCount;

    public string? DoubleClickDetectionReason { get; } = doubleClickDetectionReason;
}
