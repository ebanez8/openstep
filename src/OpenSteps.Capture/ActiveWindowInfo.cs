using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed record ActiveWindowInfo(
    IntPtr Handle,
    string? Title,
    string? ProcessName,
    string? ExecutablePath,
    ScreenBounds? Bounds);
