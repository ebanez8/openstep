namespace OpenSteps.Core.Models;

public sealed class SessionSummary
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "Untitled OpenSteps Guide";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int StepCount { get; set; }

    public string? ThumbnailPath { get; set; }
}
