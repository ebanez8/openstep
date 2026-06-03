using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class StepEditorOperationsTests
{
    [Fact]
    public void CreateManualStep_HasManualActionType()
    {
        var step = StepEditorOperations.CreateManualStep();

        Assert.Equal(StepActionType.Manual, step.ActionType);
        Assert.Equal("Manual step", step.GeneratedTitle);
        Assert.Equal("New step", step.UserTitle);
        Assert.Null(step.ScreenshotRelativePath);
    }

    [Fact]
    public void InsertManualStepBelow_RecalculatesIndexes()
    {
        var first = new RecordedStep { Index = 1, GeneratedTitle = "First" };
        var second = new RecordedStep { Index = 2, GeneratedTitle = "Second" };
        var steps = new List<RecordedStep> { first, second };

        var manual = StepEditorOperations.InsertManualStepBelow(steps, first);

        Assert.Equal([1, 2, 3], steps.Select(step => step.Index).ToArray());
        Assert.Same(manual, steps[1]);
        Assert.Equal(StepActionType.Manual, steps[1].ActionType);
        Assert.Same(second, steps[2]);
    }
}
