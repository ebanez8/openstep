using OpenSteps.Capture;

namespace OpenSteps.Tests;

public sealed class ClickTargetClassifierTests
{
    [Fact]
    public void ShellTrayWnd_ClassifiesAsTaskbarOrShell()
    {
        var info = ClickTargetClassifier.CreateInfo(
            900,
            1050,
            new IntPtr(10),
            new IntPtr(11),
            "MSTaskListWClass",
            "Shell_TrayWnd",
            "explorer",
            new HashSet<IntPtr>());

        Assert.Equal(ClickClassification.TaskbarOrShell, info.Classification);
        Assert.Equal("Skipped taskbar/shell click", info.SkipReason);
    }

    [Fact]
    public void SecondaryTrayWnd_ClassifiesAsTaskbarOrShell()
    {
        var info = ClickTargetClassifier.CreateInfo(
            900,
            1050,
            new IntPtr(10),
            new IntPtr(11),
            "Button",
            "Shell_SecondaryTrayWnd",
            "explorer",
            new HashSet<IntPtr>());

        Assert.Equal(ClickClassification.TaskbarOrShell, info.Classification);
    }

    [Fact]
    public void RegisteredOpenStepsHandle_ClassifiesAsOpenStepsWindow()
    {
        var openStepsHandle = new IntPtr(42);
        var info = ClickTargetClassifier.CreateInfo(
            50,
            50,
            new IntPtr(41),
            openStepsHandle,
            "HwndWrapper",
            "HwndWrapper",
            "OpenSteps.App",
            new HashSet<IntPtr> { openStepsHandle });

        Assert.Equal(ClickClassification.OpenStepsWindow, info.Classification);
        Assert.Equal("Skipped OpenSteps UI click", info.SkipReason);
    }

    [Fact]
    public void NormalAppWindow_ClassifiesAsRecordable()
    {
        var info = ClickTargetClassifier.CreateInfo(
            300,
            300,
            new IntPtr(20),
            new IntPtr(21),
            "Edit",
            "Notepad",
            "notepad",
            new HashSet<IntPtr>());

        Assert.Equal(ClickClassification.RecordableAppWindow, info.Classification);
    }

    [Fact]
    public void TaskbarClick_DoesNotResolveToRecordedTarget()
    {
        var target = ClickTargetClassifier.CreateInfo(
            900,
            1050,
            new IntPtr(10),
            new IntPtr(11),
            "MSTaskListWClass",
            "Shell_TrayWnd",
            "explorer",
            new HashSet<IntPtr>());

        var resolution = new CaptureTargetResolver().Resolve(target, null);

        Assert.False(resolution.ShouldRecord);
        Assert.Equal("SkippedTaskbar", resolution.ResolutionSource);
    }

    [Fact]
    public void NormalAppClick_PrefersClickedWindowRoot()
    {
        var root = new IntPtr(21);
        var target = ClickTargetClassifier.CreateInfo(
            300,
            300,
            new IntPtr(20),
            root,
            "Edit",
            "Notepad",
            "notepad",
            new HashSet<IntPtr>());

        var resolution = new CaptureTargetResolver().Resolve(target, null);

        Assert.True(resolution.ShouldRecord);
        Assert.Equal(root, resolution.TargetHwnd);
        Assert.Equal("ClickedWindowRoot", resolution.ResolutionSource);
    }
}
