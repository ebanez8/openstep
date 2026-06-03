using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public static class StepEditorOperations
{
    public static RecordedStep CreateManualStep()
    {
        return new RecordedStep
        {
            Index = 1,
            Timestamp = DateTimeOffset.Now,
            ActionType = StepActionType.Manual,
            GeneratedTitle = "Manual step",
            UserTitle = "New step",
            UserDescription = string.Empty,
            ScreenshotRelativePath = null
        };
    }

    public static RecordedStep AddManualStepAtEnd(IList<RecordedStep> steps)
    {
        return InsertManualStep(steps, steps.Count);
    }

    public static RecordedStep InsertManualStepBelow(IList<RecordedStep> steps, RecordedStep currentStep)
    {
        var index = steps.IndexOf(currentStep);
        return InsertManualStep(steps, index < 0 ? steps.Count : index + 1);
    }

    public static RecordedStep InsertManualStep(IList<RecordedStep> steps, int insertIndex)
    {
        var step = CreateManualStep();
        var clampedIndex = Math.Clamp(insertIndex, 0, steps.Count);
        steps.Insert(clampedIndex, step);
        RecalculateIndexes(steps);
        return step;
    }

    public static void RecalculateIndexes(IList<RecordedStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            steps[i].Index = i + 1;
        }
    }
}
