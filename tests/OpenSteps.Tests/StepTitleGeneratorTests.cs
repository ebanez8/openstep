using OpenSteps.Core.Models;
using OpenSteps.Core.Services;

namespace OpenSteps.Tests;

public sealed class StepTitleGeneratorTests
{
    private readonly StepTitleGenerator _generator = new();

    [Fact]
    public void Generate_UsesButtonElementName()
    {
        var step = new RecordedStep { ElementName = "Save", ControlType = "Button", UsefulElementFound = true };

        Assert.Equal("Click \"Save\"", _generator.Generate(step));
    }

    [Fact]
    public void Generate_UsesMenuItemVerb()
    {
        var step = new RecordedStep { ElementName = "File", ControlType = "MenuItem", UsefulElementFound = true };

        Assert.Equal("Select \"File\"", _generator.Generate(step));
    }

    [Fact]
    public void Generate_UsesEditFieldText()
    {
        var step = new RecordedStep { ElementName = "Search", ControlType = "Edit", UsefulElementFound = true };

        Assert.Equal("Click the \"Search\" field", _generator.Generate(step));
    }

    [Fact]
    public void Generate_FallsBackToWindowTitle()
    {
        var step = new RecordedStep { WindowTitle = "Settings" };

        Assert.Equal("Click in Settings", _generator.Generate(step));
    }

    [Fact]
    public void Generate_FallsBackToCoordinates()
    {
        var step = new RecordedStep { ClickX = 10, ClickY = 20 };

        Assert.Equal("Click at screen position (10, 20)", _generator.Generate(step));
    }

    [Fact]
    public void Generate_FallsBackToWindowForGenericContainer()
    {
        var step = new RecordedStep
        {
            WindowTitle = "config.toml - Notepad",
            ControlType = "Pane",
            ClassName = "Microsoft.UI.Content.DesktopChildSiteBridge",
            UsefulElementFound = false
        };

        var result = _generator.GenerateWithReason(step);

        Assert.Equal("Click in config.toml - Notepad", result.Title);
        Assert.Equal("window title fallback", result.Reason);
    }

    [Fact]
    public void Generate_FallsBackToProcessNameWhenWindowMissing()
    {
        var step = new RecordedStep { ProcessName = "Notepad", ClickX = 10, ClickY = 20 };

        Assert.Equal("Click in Notepad", _generator.Generate(step));
    }

    [Fact]
    public void Generate_CreatesTextEntryTitleForNamedTarget()
    {
        var step = new RecordedStep
        {
            ActionType = StepActionType.TextEntry,
            InputTargetName = "Search",
            KeyboardInputDetected = true,
            KeyCount = 12
        };

        Assert.Equal("Type into \"Search\"", _generator.Generate(step));
    }

    [Fact]
    public void Generate_CreatesTextEntryWindowFallback()
    {
        var step = new RecordedStep
        {
            ActionType = StepActionType.TextEntry,
            WindowTitle = "config.toml - Notepad",
            KeyboardInputDetected = true,
            KeyCount = 30
        };

        Assert.Equal("Enter text in config.toml - Notepad", _generator.Generate(step));
    }

    [Fact]
    public void Generate_CreatesShortcutTitle()
    {
        var step = new RecordedStep
        {
            ActionType = StepActionType.Shortcut,
            ShortcutName = "Ctrl+S",
            KeyboardInputDetected = true,
            KeyCount = 1
        };

        Assert.Equal("Press Ctrl+S", _generator.Generate(step));
    }

    [Fact]
    public void Generate_CreatesSpecialKeyTitle()
    {
        var step = new RecordedStep
        {
            ActionType = StepActionType.SpecialKey,
            SpecialKeyName = "Enter",
            KeyboardInputDetected = true,
            KeyCount = 1
        };

        Assert.Equal("Press Enter", _generator.Generate(step));
    }

    [Fact]
    public void Generate_UsesSensitiveTextTitle()
    {
        var step = new RecordedStep
        {
            ActionType = StepActionType.TextEntry,
            InputTargetName = "Password",
            IsSensitiveInput = true,
            KeyboardInputDetected = true
        };

        var result = _generator.GenerateWithReason(step);

        Assert.Equal("Enter hidden text", result.Title);
        Assert.Equal("sensitive text entry", result.Reason);
    }
}
