using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class StepTitleGenerator
{
    public string Generate(RecordedStep step)
    {
        var name = Clean(step.ElementName);
        var controlType = Clean(step.ControlType);

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (IsControlType(controlType, "Button"))
            {
                return $"Click \"{name}\"";
            }

            if (IsControlType(controlType, "MenuItem"))
            {
                return $"Select \"{name}\"";
            }

            if (IsControlType(controlType, "Edit"))
            {
                return $"Click the \"{name}\" field";
            }

            return $"Click \"{name}\"";
        }

        if (!string.IsNullOrWhiteSpace(step.WindowTitle))
        {
            return $"Click in {step.WindowTitle}";
        }

        return $"Click at screen position ({step.ClickX}, {step.ClickY})";
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
