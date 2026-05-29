namespace OpenSteps.Core.Models;

public sealed class RecordedStep
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Index { get; set; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string? ScreenshotPath { get; set; }

    public int ClickX { get; set; }

    public int ClickY { get; set; }

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

    public string DisplayTitle => string.IsNullOrWhiteSpace(UserTitle) ? GeneratedTitle : UserTitle!;

    public string MetadataSummary
    {
        get
        {
            var lines = new List<string>
            {
                $"Click: ({ClickX}, {ClickY})",
                $"Window: {WindowTitle ?? "(unknown)"}",
                $"Process: {ProcessName ?? "(unknown)"}",
                $"Element: {ElementName ?? "(unknown)"}",
                $"Control: {ControlType ?? "(unknown)"}",
                $"AutomationId: {AutomationId ?? "(none)"}",
                $"Class: {ClassName ?? "(none)"}"
            };

            if (!string.IsNullOrWhiteSpace(ParentElementName))
            {
                lines.Add($"Parent: {ParentElementName}");
            }

            if (!string.IsNullOrWhiteSpace(CaptureError))
            {
                lines.Add($"Error: {CaptureError}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
