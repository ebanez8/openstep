using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed record UiElementInfo(
    string? Name,
    string? AutomationId,
    string? ControlType,
    string? ClassName,
    ScreenBounds? Bounds,
    string? ParentName,
    UiAutomationQuality Quality,
    bool UsefulElementFound,
    string RawElementDebug,
    string ParentChainDebug,
    string CandidateElementsDebug);
