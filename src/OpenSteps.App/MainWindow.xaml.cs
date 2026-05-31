using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using OpenSteps.Capture;
using OpenSteps.Core.Models;
using OpenSteps.Core.Services;
using WinForms = System.Windows.Forms;

namespace OpenSteps.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ActiveWindowService _activeWindowService = new();
    private readonly ScreenshotService _screenshotService = new();
    private readonly UiAutomationService _uiAutomationService = new();
    private readonly DpiAwarenessService _dpiAwarenessService = new();
    private readonly StepTitleGenerator _titleGenerator = new();
    private readonly MarkdownExporter _markdownExporter = new();
    private readonly SessionStore _sessionStore = new();
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly DispatcherTimer _typingTimer;

    private RecordingSession _session = new();
    private GlobalMouseHook? _mouseHook;
    private GlobalKeyboardHook? _keyboardHook;
    private RecordingToolbarWindow? _toolbar;
    private RecordedStep? _pendingTypingStep;
    private int _pendingTypingKeyCount;
    private bool _isRecording;
    private bool _isPaused;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
        _typingTimer.Tick += (_, _) => FinalizePendingTyping();
        ResetSession();
        Closing += MainWindow_Closing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RecordedStep> Steps { get; } = [];

    private async void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        await StartRecordingAsync();
    }

    private async void CaptureTestStep_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRecording)
        {
            EnsureSession();
        }

        var point = PointToScreen(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2));
        await CaptureStepAsync((int)point.X, (int)point.Y);
        ShowEditor();
    }

    private async Task StartRecordingAsync()
    {
        EnsureSession();
        StopHooks();
        _isRecording = true;
        _isPaused = false;

        try
        {
            _toolbar?.Close();
            _toolbar = new RecordingToolbarWindow();
            _toolbar.PauseToggled += (_, _) => _isPaused = _toolbar.IsPaused;
            _toolbar.StopRequested += async (_, _) => await StopRecordingAsync();
            _toolbar.SetStepCount(Steps.Count);
            _toolbar.Show();
            _toolbar.StartClock();

            _mouseHook = new GlobalMouseHook(ShouldIgnoreClick);
            _mouseHook.ClickCaptured += MouseHook_ClickCaptured;
            _mouseHook.Start();

            _keyboardHook = new GlobalKeyboardHook(ShouldIgnoreKeyboard);
            _keyboardHook.KeyboardInputCaptured += KeyboardHook_KeyboardInputCaptured;
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            CleanupRecordingUi();
            _isRecording = false;
            _isPaused = false;
            WinForms.MessageBox.Show($"Recording could not start:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            return;
        }

        WindowState = WindowState.Minimized;
        await Task.CompletedTask;
    }

    private bool ShouldIgnoreClick(int x, int y)
    {
        return _toolbar?.ContainsScreenPoint(x, y) == true || ContainsMainWindowPoint(x, y);
    }

    private void MouseHook_ClickCaptured(object? sender, ClickCapturedEventArgs e)
    {
        if (!_isRecording || _isPaused)
        {
            return;
        }

        Dispatcher.BeginInvoke(async () => await CaptureStepAsync(e.X, e.Y));
    }

    private void KeyboardHook_KeyboardInputCaptured(object? sender, KeyboardInputEventArgs e)
    {
        if (!_isRecording || _isPaused)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => HandleKeyboardInput(e));
    }

    private async Task CaptureStepAsync(int x, int y)
    {
        if (!await _captureLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            EnsureSession();
            var step = new RecordedStep
            {
                Index = Steps.Count + 1,
                ClickX = x,
                ClickY = y,
                VirtualScreenBounds = GetVirtualScreenBounds(),
                ProcessDpiAwareness = _dpiAwarenessService.GetCurrentThreadAwareness()
            };

            try
            {
                var activeWindow = _activeWindowService.Capture();
                step.ActiveWindowHandle = activeWindow.Handle;
                step.WindowTitle = activeWindow.Title;
                step.ProcessName = activeWindow.ProcessName;
                step.ExecutablePath = activeWindow.ExecutablePath;
                step.WindowBounds = activeWindow.Bounds;
                step.ClickInsideActiveWindowBounds = ContainsBounds(activeWindow.Bounds, x, y);
            }
            catch (Exception ex)
            {
                step.CaptureError = AppendError(step.CaptureError, $"Window metadata failed: {ex.Message}");
            }

            try
            {
                var element = _uiAutomationService.GetElementAt(x, y);
                step.UiAutomationSucceeded = element.Quality != UiAutomationQuality.UiAutomationFailed;
                step.UiAutomationQuality = element.Quality;
                step.UsefulElementFound = element.UsefulElementFound;
                step.RawElementDebug = element.RawElementDebug;
                step.ParentChainDebug = element.ParentChainDebug;
                step.CandidateElementsDebug = element.CandidateElementsDebug;
                if (element is not null)
                {
                    step.ElementName = element.Name;
                    step.AutomationId = element.AutomationId;
                    step.ControlType = element.ControlType;
                    step.ClassName = element.ClassName;
                    step.ElementBounds = element.Bounds;
                step.ParentElementName = element.ParentName;
                    step.InputTargetName = element.IsEditable && element.UsefulElementFound ? element.Name : null;
                    step.InputTargetControlType = element.ControlType;
                    step.IsSensitiveInput = element.IsPassword;
                }
            }
            catch (Exception ex)
            {
                step.CaptureError = AppendError(step.CaptureError, $"UI Automation failed: {ex.Message}");
            }

            var title = _titleGenerator.GenerateWithReason(step);
            step.GeneratedTitle = title.Title;
            step.GeneratedTitleReason = title.Reason;
            step.UserTitle = step.GeneratedTitle;

            try
            {
                step.ScreenshotPath = await _screenshotService.CaptureVirtualDesktopAsync(_session.OutputDirectory, step.Index, x, y);
                step.ScreenshotCaptured = true;
            }
            catch (Exception ex)
            {
                step.ScreenshotCaptured = false;
                step.CaptureError = AppendError(step.CaptureError, $"Screenshot failed: {ex.Message}");
            }

            Steps.Add(step);
            RenumberSteps();
            _toolbar?.SetStepCount(Steps.Count);
            UpdateSessionSummary();
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private void HandleKeyboardInput(KeyboardInputEventArgs input)
    {
        if (!_isRecording || _isPaused || ShouldIgnoreKeyboard())
        {
            return;
        }

        if (input.Kind == KeyboardInputKind.Text)
        {
            AddTextKeyToPendingStep();
            return;
        }

        FinalizePendingTyping();
        var actionType = input.Kind == KeyboardInputKind.Shortcut ? StepActionType.Shortcut : StepActionType.SpecialKey;
        var step = CreateKeyboardActionStep(actionType, input.KeyName, input.ShortcutName);
        Steps.Add(step);
        RenumberSteps();
        _toolbar?.SetStepCount(Steps.Count);
        UpdateSessionSummary();
    }

    private void AddTextKeyToPendingStep()
    {
        var step = _pendingTypingStep ?? CreateOrUpdateTextEntryStep();
        _pendingTypingStep = step;
        _pendingTypingKeyCount++;
        step.KeyboardInputDetected = true;
        step.TypedCharactersStored = false;
        step.KeyCount = step.IsSensitiveInput ? null : _pendingTypingKeyCount;

        var title = _titleGenerator.GenerateWithReason(step);
        step.GeneratedTitle = title.Title;
        step.GeneratedTitleReason = title.Reason;
        step.UserTitle = step.GeneratedTitle;

        StepsList.Items.Refresh();
        _typingTimer.Stop();
        _typingTimer.Start();
    }

    private RecordedStep CreateOrUpdateTextEntryStep()
    {
        var previous = Steps.LastOrDefault();
        if (previous is not null && previous.ActionType == StepActionType.Click)
        {
            ApplyFocusedInputTarget(previous);
            previous.ActionType = StepActionType.TextEntry;
            previous.KeyboardInputDetected = true;
            previous.TypedCharactersStored = false;
            previous.InputTargetName = IsEditableClickTarget(previous) && previous.UsefulElementFound ? previous.ElementName : previous.InputTargetName;
            previous.InputTargetControlType = previous.ControlType;
            return previous;
        }

        var step = CreateKeyboardActionStep(StepActionType.TextEntry, null, null);
        Steps.Add(step);
        RenumberSteps();
        _toolbar?.SetStepCount(Steps.Count);
        return step;
    }

    private void ApplyFocusedInputTarget(RecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.InputTargetName) || step.IsSensitiveInput)
        {
            return;
        }

        try
        {
            var focused = _uiAutomationService.GetFocusedElement();
            if (focused.IsEditable || focused.IsPassword)
            {
                step.InputTargetName = focused.UsefulElementFound ? focused.Name : null;
                step.InputTargetControlType = focused.ControlType;
                step.IsSensitiveInput = focused.IsPassword;
                step.RawElementDebug = AppendDebug(step.RawElementDebug, "Focused element:", focused.RawElementDebug);
                step.ParentChainDebug = AppendDebug(step.ParentChainDebug, "Focused parent chain:", focused.ParentChainDebug);
            }
        }
        catch (Exception ex)
        {
            step.CaptureError = AppendError(step.CaptureError, $"Focused UI Automation failed: {ex.Message}");
        }
    }

    private RecordedStep CreateKeyboardActionStep(StepActionType actionType, string? specialKeyName, string? shortcutName)
    {
        var previous = Steps.LastOrDefault();
        var step = new RecordedStep
        {
            Index = Steps.Count + 1,
            ActionType = actionType,
            KeyboardInputDetected = true,
            KeyCount = actionType == StepActionType.TextEntry ? 0 : 1,
            SpecialKeyName = specialKeyName,
            ShortcutName = shortcutName,
            TypedCharactersStored = false,
            ScreenshotPath = previous?.ScreenshotPath,
            ScreenshotCaptured = previous?.ScreenshotCaptured == true,
            VirtualScreenBounds = GetVirtualScreenBounds(),
            ProcessDpiAwareness = _dpiAwarenessService.GetCurrentThreadAwareness(),
            WindowTitle = previous?.WindowTitle,
            ProcessName = previous?.ProcessName,
            ExecutablePath = previous?.ExecutablePath,
            WindowBounds = previous?.WindowBounds,
            InputTargetName = previous?.InputTargetName,
            InputTargetControlType = previous?.InputTargetControlType,
            IsSensitiveInput = previous?.IsSensitiveInput == true
        };

        if (actionType == StepActionType.TextEntry)
        {
            ApplyFocusedInputTarget(step);
        }

        try
        {
            var activeWindow = _activeWindowService.Capture();
            step.ActiveWindowHandle = activeWindow.Handle;
            step.WindowTitle = activeWindow.Title ?? step.WindowTitle;
            step.ProcessName = activeWindow.ProcessName ?? step.ProcessName;
            step.ExecutablePath = activeWindow.ExecutablePath ?? step.ExecutablePath;
            step.WindowBounds = activeWindow.Bounds ?? step.WindowBounds;
        }
        catch (Exception ex)
        {
            step.CaptureError = AppendError(step.CaptureError, $"Window metadata failed: {ex.Message}");
        }

        var title = _titleGenerator.GenerateWithReason(step);
        step.GeneratedTitle = title.Title;
        step.GeneratedTitleReason = title.Reason;
        step.UserTitle = step.GeneratedTitle;
        return step;
    }

    private void FinalizePendingTyping()
    {
        _typingTimer.Stop();
        _pendingTypingStep = null;
        _pendingTypingKeyCount = 0;
        StepsList.Items.Refresh();
    }

    private async Task StopRecordingAsync()
    {
        _isRecording = false;
        _isPaused = false;
        FinalizePendingTyping();
        StopHooks();
        CleanupRecordingUi();

        WindowState = WindowState.Normal;
        Show();
        Activate();
        ShowEditor();
        await SaveSessionAsync(showMessage: false);
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose a folder for guide.md and images",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        try
        {
            SyncSessionOrder();
            _session.Title = SessionTitleBox.Text;
            await SaveSessionAsync(showMessage: false);
            var path = await _markdownExporter.ExportAsync(_session, dialog.SelectedPath);
            var result = WinForms.MessageBox.Show(
                $"Exported guide:\n{path}\n\nOpen the export folder now?",
                "OpenSteps",
                WinForms.MessageBoxButtons.YesNo,
                WinForms.MessageBoxIcon.Information);

            if (result == WinForms.DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Export failed:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    private async void SaveSession_Click(object sender, RoutedEventArgs e)
    {
        await SaveSessionAsync(showMessage: true);
    }

    private async void OpenPreviousSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var session = await _sessionStore.LoadLatestAsync();
            if (session is null)
            {
                WinForms.MessageBox.Show("No saved OpenSteps sessions were found.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            LoadSession(session);
            ShowEditor();
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Could not open the latest session:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is RecordedStep step)
        {
            Steps.Remove(step);
            _session.Steps.Remove(step);
            RenumberSteps();
            UpdateSessionSummary();
            _ = SaveSessionAsync(showMessage: false);
        }
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
            SyncSessionOrder();
            _ = SaveSessionAsync(showMessage: false);
        }
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        if (index >= 0 && index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
            SyncSessionOrder();
            _ = SaveSessionAsync(showMessage: false);
        }
    }

    private void SessionTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_session is not null)
        {
            _session.Title = SessionTitleBox.Text;
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        StopHooks();
        CleanupRecordingUi();
        if (Steps.Count > 0)
        {
            try
            {
                await SaveSessionAsync(showMessage: false);
            }
            catch
            {
                // Closing should never be blocked by persistence failures.
            }
        }
    }

    private void EnsureSession()
    {
        if (!string.IsNullOrWhiteSpace(_session.OutputDirectory))
        {
            return;
        }

        ResetSession();
    }

    private void ResetSession()
    {
        _session = new RecordingSession
        {
            Title = $"OpenSteps Guide {DateTimeOffset.Now:yyyy-MM-dd HH.mm}",
            OutputDirectory = Path.Combine(Path.GetTempPath(), "OpenSteps", DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss"))
        };

        Directory.CreateDirectory(_session.OutputDirectory);
        Steps.Clear();
        SessionTitleBox.Text = _session.Title;
        UpdateSessionSummary();
    }

    private void LoadSession(RecordingSession session)
    {
        StopHooks();
        CleanupRecordingUi();
        _isRecording = false;
        _isPaused = false;
        _session = session;
        Steps.Clear();
        foreach (var step in _session.Steps.OrderBy(step => step.Index))
        {
            Steps.Add(step);
        }

        RenumberSteps();
        SessionTitleBox.Text = _session.Title;
        UpdateSessionSummary();
    }

    private void ShowEditor()
    {
        HomePanel.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Visible;
        SessionTitleBox.Text = _session.Title;
        UpdateSessionSummary();
    }

    private void RenumberSteps()
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].Index = i + 1;
        }

        SyncSessionOrder();
        StepsList.Items.Refresh();
    }

    private void SyncSessionOrder()
    {
        _session.Steps.Clear();
        foreach (var step in Steps)
        {
            _session.Steps.Add(step);
        }

        for (var i = 0; i < _session.Steps.Count; i++)
        {
            _session.Steps[i].Index = i + 1;
        }

        StepsList.Items.Refresh();
        UpdateSessionSummary();
    }

    private void UpdateSessionSummary()
    {
        SessionSummaryText.Text = $"{Steps.Count} captured step{(Steps.Count == 1 ? string.Empty : "s")} - Local session folder: {_session.OutputDirectory}";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Steps)));
    }

    private async Task SaveSessionAsync(bool showMessage)
    {
        SyncSessionOrder();
        _session.Title = SessionTitleBox.Text;
        var path = await _sessionStore.SaveAsync(_session);
        UpdateSessionSummary();

        if (showMessage)
        {
            WinForms.MessageBox.Show($"Saved session:\n{path}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
        }
    }

    private void StopHooks()
    {
        try
        {
            if (_mouseHook is not null)
            {
                _mouseHook.ClickCaptured -= MouseHook_ClickCaptured;
                _mouseHook.Dispose();
            }
        }
        finally
        {
            _mouseHook = null;
        }

        try
        {
            if (_keyboardHook is not null)
            {
                _keyboardHook.KeyboardInputCaptured -= KeyboardHook_KeyboardInputCaptured;
                _keyboardHook.Dispose();
            }
        }
        finally
        {
            _keyboardHook = null;
        }
    }

    private void CleanupRecordingUi()
    {
        try
        {
            _toolbar?.Close();
        }
        finally
        {
            _toolbar = null;
        }
    }

    private bool ContainsMainWindowPoint(int x, int y)
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            return false;
        }

        var point = PointFromScreen(new System.Windows.Point(x, y));
        return point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight;
    }

    private bool ShouldIgnoreKeyboard()
    {
        var foreground = NativeMethodsForApp.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        var mainHandle = new WindowInteropHelper(this).Handle;
        if (foreground == mainHandle)
        {
            return true;
        }

        return _toolbar?.IsForegroundWindow(foreground) == true;
    }

    private static bool IsEditableClickTarget(RecordedStep step)
    {
        return step.IsSensitiveInput
            || !string.IsNullOrWhiteSpace(step.InputTargetName)
            || step.ControlType?.Contains("Edit", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ScreenBounds GetVirtualScreenBounds()
    {
        var bounds = WinForms.SystemInformation.VirtualScreen;
        return new ScreenBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static bool ContainsBounds(ScreenBounds? bounds, int x, int y)
    {
        return bounds is { } value
            && x >= value.X
            && y >= value.Y
            && x <= value.X + value.Width
            && y <= value.Y + value.Height;
    }

    private static string AppendError(string? existing, string next)
    {
        return string.IsNullOrWhiteSpace(existing) ? next : $"{existing}; {next}";
    }

    private static string AppendDebug(string? existing, string label, string next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return existing ?? string.Empty;
        }

        var block = $"{label}{Environment.NewLine}{next}";
        return string.IsNullOrWhiteSpace(existing) ? block : $"{existing}{Environment.NewLine}{Environment.NewLine}{block}";
    }
}
