namespace OpenSteps.Core.Models;

public sealed class ScreenshotAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ScreenshotAnnotationType Type { get; set; }

    public double X1 { get; set; }

    public double Y1 { get; set; }

    public double X2 { get; set; }

    public double Y2 { get; set; }

    public string? Text { get; set; }

    public string Color { get; set; } = "#D92D20";

    public double Opacity { get; set; } = 1;

    public double StrokeThickness { get; set; } = 3;
}
