namespace OpenSteps.Capture;

public sealed class ClickTargetInfo
{
    public int X { get; set; }

    public int Y { get; set; }

    public IntPtr HitHwnd { get; set; }

    public IntPtr RootHwnd { get; set; }

    public string? HitClassName { get; set; }

    public string? RootClassName { get; set; }

    public string? ProcessName { get; set; }

    public IReadOnlyList<string> AncestorClassNames { get; set; } = [];

    public ClickClassification Classification { get; set; }

    public string? SkipReason { get; set; }
}
