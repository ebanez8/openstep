using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class StepTitleGenerator
{
    public string Generate(RecordedStep step)
    {
        return GenerateWithReason(step).Title;
    }

    public StepTitleResult GenerateWithReason(RecordedStep step)
    {
        var name = Clean(step.ElementName);
        var controlType = Clean(step.ControlType);

        if (step.UsefulElementFound && !string.IsNullOrWhiteSpace(name))
        {
            if (IsControlType(controlType, "Button"))
            {
                return new StepTitleResult($"Click \"{name}\"", "useful Button element name");
            }

            if (IsControlType(controlType, "MenuItem"))
            {
                return new StepTitleResult($"Select \"{name}\"", "useful MenuItem element name");
            }

            if (IsControlType(controlType, "Edit"))
            {
                return new StepTitleResult($"Click the \"{name}\" field", "useful Edit element name");
            }

            return new StepTitleResult($"Click \"{name}\"", "useful UI Automation element name");
        }

        if (!string.IsNullOrWhiteSpace(step.WindowTitle))
        {
            return new StepTitleResult($"Click in {step.WindowTitle}", "window title fallback");
        }

        if (!string.IsNullOrWhiteSpace(step.ProcessName))
        {
            return new StepTitleResult($"Click in {step.ProcessName}", "process name fallback");
        }

        return new StepTitleResult($"Click at screen position ({step.ClickX}, {step.ClickY})", "coordinate fallback");
    }

    private static bool IsControlType(string? actual, string expected)
    {
        return actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record StepTitleResult(string Title, string Reason);
