namespace OpenSteps.Core.Services;

public sealed class ExportResult
{
    public string ExportFolder { get; set; } = string.Empty;

    public string MarkdownPath { get; set; } = string.Empty;

    public string HtmlPath { get; set; } = string.Empty;

    public IReadOnlyList<string> OutputPaths { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}
