namespace OpenSteps.Core.Services;

public sealed class ExportResult
{
    public string ExportFolder { get; set; } = string.Empty;

    public string MarkdownPath { get; set; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; set; } = [];
}
