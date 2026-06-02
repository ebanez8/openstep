namespace OpenSteps.Core.Models;

public sealed class RecordingSession
{
    public int SchemaVersion { get; set; } = 1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public string Title { get; set; } = "Untitled OpenSteps Guide";

    public string SessionDirectory { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public List<RecordedStep> Steps { get; set; } = [];
}
