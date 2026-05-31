using System.Text.Json.Serialization;

namespace OpenSteps.Core.Models;

public sealed class RecordedStep
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Index { get; set; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public StepActionType ActionType { get; set; } = StepActionType.Click;

    public string? ScreenshotPath { get; set; }

    public int ClickX { get; set; }

    public int ClickY { get; set; }

    public int? LocalClickX { get; set; }

    public int? LocalClickY { get; set; }

    public string? MonitorDeviceName { get; set; }

    public int? MonitorIndex { get; set; }

    public int? MonitorBoundsLeft { get; set; }

    public int? MonitorBoundsTop { get; set; }

    public int? MonitorBoundsRight { get; set; }

    public int? MonitorBoundsBottom { get; set; }

    public int? MonitorWidth { get; set; }

    public int? MonitorHeight { get; set; }

    public bool? IsPrimaryMonitor { get; set; }

    public uint? MonitorDpiX { get; set; }

    public uint? MonitorDpiY { get; set; }

    public ScreenshotCaptureMode ScreenshotCaptureMode { get; set; } = ScreenshotCaptureMode.MonitorContainingClick;

    public int? ScreenshotWidth { get; set; }

    public int? ScreenshotHeight { get; set; }

    [JsonIgnore]
    public IntPtr ActiveWindowHandle { get; set; }

    public string? WindowTitle { get; set; }

    public string? ProcessName { get; set; }

    public string? ExecutablePath { get; set; }

    public ScreenBounds? WindowBounds { get; set; }

    public string? ElementName { get; set; }

    public string? AutomationId { get; set; }

    public string? ControlType { get; set; }

    public string? ClassName { get; set; }

    public ScreenBounds? ElementBounds { get; set; }

    public string? ParentElementName { get; set; }

    public string GeneratedTitle { get; set; } = "Click";

    public string? UserTitle { get; set; }

    public string? UserDescription { get; set; }

    public string? CaptureError { get; set; }

    public bool ScreenshotCaptured { get; set; }

    public bool UiAutomationSucceeded { get; set; }

    public UiAutomationQuality UiAutomationQuality { get; set; } = UiAutomationQuality.UiAutomationFailed;

    public bool UsefulElementFound { get; set; }

    public string? RawElementDebug { get; set; }

    public string? ParentChainDebug { get; set; }

    public string? CandidateElementsDebug { get; set; }

    public string? GeneratedTitleReason { get; set; }

    public ScreenBounds? VirtualScreenBounds { get; set; }

    public bool? ClickInsideActiveWindowBounds { get; set; }

    public string? ProcessDpiAwareness { get; set; }

    public bool KeyboardInputDetected { get; set; }

    public int? KeyCount { get; set; }

    public string? SpecialKeyName { get; set; }

    public string? ShortcutName { get; set; }

    public bool IsSensitiveInput { get; set; }

    public string? InputTargetName { get; set; }

    public string? InputTargetControlType { get; set; }

    public bool TypedCharactersStored { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(UserTitle) ? GeneratedTitle : UserTitle!;

    public string MetadataSummary
    {
        get
        {
            var lines = new List<string>
            {
                $"Click absolute: ({ClickX}, {ClickY})",
                $"Click local in screenshot: {FormatPoint(LocalClickX, LocalClickY)}",
                $"Detected monitor: {MonitorDeviceName ?? "(unknown)"}",
                $"Monitor index: {(MonitorIndex.HasValue ? MonitorIndex.Value.ToString() : "(unknown)")}",
                $"Primary monitor: {FormatBool(IsPrimaryMonitor)}",
                $"Monitor bounds: {FormatMonitorBounds()}",
                $"Monitor size: {FormatSize(MonitorWidth, MonitorHeight)}",
                $"Monitor DPI: {FormatDpi()}",
                $"Capture mode: {ScreenshotCaptureMode}",
                $"Action type: {ActionType}",
                $"Keyboard input detected: {(KeyboardInputDetected ? "yes" : "no")}",
                $"Key count: {(KeyCount.HasValue ? KeyCount.Value.ToString() : "(not stored)")}",
                $"Special key: {SpecialKeyName ?? "(none)"}",
                $"Shortcut: {ShortcutName ?? "(none)"}",
                $"Input target: {InputTargetName ?? "(unknown)"}",
                $"Input target control: {InputTargetControlType ?? "(unknown)"}",
                $"Sensitive input: {(IsSensitiveInput ? "yes" : "no")}",
                $"Typed characters stored: {(TypedCharactersStored ? "yes" : "no")}",
                $"Virtual screen: {FormatBounds(VirtualScreenBounds)}",
                $"Screenshot size: {FormatSize(ScreenshotWidth, ScreenshotHeight)}",
                $"Active window bounds: {FormatBounds(WindowBounds)}",
                $"Click inside active window: {FormatBool(ClickInsideActiveWindowBounds)}",
                $"Process DPI awareness: {ProcessDpiAwareness ?? "(unknown)"}",
                $"Screenshot captured: {(ScreenshotCaptured ? "yes" : "no")}",
                $"UI Automation status: {UiAutomationQuality}",
                $"UI Automation returned element: {(UiAutomationSucceeded ? "yes" : "no")}",
                $"Useful element found: {(UsefulElementFound ? "yes" : "no")}",
                $"Screenshot: {ScreenshotPath ?? "(none)"}",
                $"Window: {WindowTitle ?? "(unknown)"}",
                $"Process: {ProcessName ?? "(unknown)"}",
                $"Element: {ElementName ?? "(unknown)"}",
                $"Control: {ControlType ?? "(unknown)"}",
                $"AutomationId: {AutomationId ?? "(none)"}",
                $"Class: {ClassName ?? "(none)"}",
                $"Element bounds: {FormatBounds(ElementBounds)}",
                $"Generated title reason: {GeneratedTitleReason ?? "(unknown)"}"
            };

            if (!string.IsNullOrWhiteSpace(ParentElementName))
            {
                lines.Add($"Parent: {ParentElementName}");
            }

            if (!string.IsNullOrWhiteSpace(RawElementDebug))
            {
                lines.Add("");
                lines.Add("Raw element:");
                lines.Add(RawElementDebug);
            }

            if (!string.IsNullOrWhiteSpace(ParentChainDebug))
            {
                lines.Add("");
                lines.Add("Parent chain:");
                lines.Add(ParentChainDebug);
            }

            if (!string.IsNullOrWhiteSpace(CandidateElementsDebug))
            {
                lines.Add("");
                lines.Add("Candidate child elements:");
                lines.Add(CandidateElementsDebug);
            }

            if (!string.IsNullOrWhiteSpace(CaptureError))
            {
                lines.Add($"Error: {CaptureError}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string FormatBounds(ScreenBounds? bounds)
    {
        return bounds is { } value
            ? $"({value.X}, {value.Y}) {value.Width}x{value.Height}"
            : "(unknown)";
    }

    private string FormatMonitorBounds()
    {
        return MonitorBoundsLeft.HasValue
            && MonitorBoundsTop.HasValue
            && MonitorBoundsRight.HasValue
            && MonitorBoundsBottom.HasValue
            ? $"left={MonitorBoundsLeft}, top={MonitorBoundsTop}, right={MonitorBoundsRight}, bottom={MonitorBoundsBottom}"
            : "(unknown)";
    }

    private static string FormatPoint(int? x, int? y)
    {
        return x.HasValue && y.HasValue ? $"({x}, {y})" : "(unknown)";
    }

    private static string FormatSize(int? width, int? height)
    {
        return width.HasValue && height.HasValue ? $"{width}x{height}" : "(unknown)";
    }

    private string FormatDpi()
    {
        return MonitorDpiX.HasValue && MonitorDpiY.HasValue ? $"{MonitorDpiX}x{MonitorDpiY}" : "(unknown)";
    }

    private static string FormatBool(bool? value)
    {
        return value.HasValue ? (value.Value ? "yes" : "no") : "(unknown)";
    }
}
