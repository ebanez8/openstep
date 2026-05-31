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
        if (step.ActionType == StepActionType.Shortcut && !string.IsNullOrWhiteSpace(step.ShortcutName))
        {
            return new StepTitleResult($"Press {step.ShortcutName}", "keyboard shortcut");
        }

        if (step.ActionType == StepActionType.SpecialKey && !string.IsNullOrWhiteSpace(step.SpecialKeyName))
        {
            return new StepTitleResult($"Press {step.SpecialKeyName}", "special key");
        }

        if (step.ActionType == StepActionType.TextEntry)
        {
            if (step.IsSensitiveInput)
            {
                return new StepTitleResult("Enter hidden text", "sensitive text entry");
            }

            var target = Clean(step.InputTargetName);
            if (!string.IsNullOrWhiteSpace(target))
            {
                return new StepTitleResult($"Type into \"{target}\"", "named text input target");
            }

            if (!string.IsNullOrWhiteSpace(step.WindowTitle))
            {
                return new StepTitleResult($"Enter text in {step.WindowTitle}", "text entry window title fallback");
            }

            if (!string.IsNullOrWhiteSpace(step.ProcessName))
            {
                return new StepTitleResult($"Enter text in {step.ProcessName}", "text entry process name fallback");
            }

            return new StepTitleResult("Enter text", "text entry fallback");
        }

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
