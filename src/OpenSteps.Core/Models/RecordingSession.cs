namespace OpenSteps.Core.Models;

public sealed class RecordingSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string Title { get; set; } = "Untitled OpenSteps Guide";

    public string OutputDirectory { get; set; } = string.Empty;

    public List<RecordedStep> Steps { get; } = [];
}
