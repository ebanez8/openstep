using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenSteps.Capture;
using OpenSteps.Core.Models;
using OpenSteps.Core.Services;
using WinForms = System.Windows.Forms;

namespace OpenSteps.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ActiveWindowService _activeWindowService = new();
    private readonly MonitorService _monitorService = new();
    private readonly ScreenshotService _screenshotService;
    private readonly UiAutomationService _uiAutomationService = new();
    private readonly DpiAwarenessService _dpiAwarenessService = new();
    private readonly ScreenshotRedactionService _redactionService = new();
    private readonly StepTitleGenerator _titleGenerator = new();
    private readonly MarkdownExporter _markdownExporter = new();
    private readonly SessionStore _sessionStore = new();
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly DispatcherTimer _typingTimer;
    private readonly DispatcherTimer _recordingStatusTimer;

    private RecordingSession _session = new();
    private GlobalMouseHook? _mouseHook;
    private GlobalKeyboardHook? _keyboardHook;
    private RecordedStep? _pendingTypingStep;
    private SessionEditorWindow? _editorWindow;
    private int _pendingTypingKeyCount;
    private bool _isRecording;
    private bool _isPaused;
    private DateTimeOffset _recordingStartedAt;
    private ScreenshotCaptureMode _screenshotCaptureMode = ScreenshotCaptureMode.MonitorContainingClick;

    public MainWindow()
    {
        _screenshotService = new ScreenshotService(_monitorService);
        InitializeComponent();
        DataContext = this;
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
        _typingTimer.Tick += async (_, _) => await FinalizePendingTypingAsync();
        _recordingStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordingStatusTimer.Tick += (_, _) => RefreshRecordingStatus();
        ResetSession();
        Closing += MainWindow_Closing;
        RefreshRecordingStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RecordedStep> Steps { get; } = [];

    public ScreenshotCaptureMode[] ScreenshotCaptureModes { get; } =
    [
        ScreenshotCaptureMode.MonitorContainingClick,
        ScreenshotCaptureMode.FullVirtualDesktop
    ];

    public ScreenshotCaptureMode ScreenshotCaptureMode
    {
        get => _screenshotCaptureMode;
        set
        {
            if (_screenshotCaptureMode == value)
            {
                return;
            }

            _screenshotCaptureMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScreenshotCaptureMode)));
        }
    }

    private async void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        await StartRecordingAsync();
    }

    private void MoveControl_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PauseRecording_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        RefreshRecordingStatus();
    }

    private async void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        await StopRecordingAsync();
    }

    private async void CaptureTestStep_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRecording)
        {
            EnsureSession();
        }

        var point = GetTestCapturePoint();
        await CaptureStepAsync(point.X, point.Y);
        ShowSessionEditor();
    }

    private async Task StartRecordingAsync()
    {
        EnsureSession();
        StopHooks();
        _isRecording = true;
        _isPaused = false;

        try
        {
            _recordingStartedAt = DateTimeOffset.Now;
            _recordingStatusTimer.Start();
            RefreshRecordingStatus();

            _mouseHook = new GlobalMouseHook(ShouldIgnoreClick);
            _mouseHook.ClickCaptured += MouseHook_ClickCaptured;
            _mouseHook.Start();

            _keyboardHook = new GlobalKeyboardHook(ShouldIgnoreKeyboard);
            _keyboardHook.KeyboardInputCaptured += KeyboardHook_KeyboardInputCaptured;
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            _isRecording = false;
            _isPaused = false;
            RefreshRecordingStatus();
            WinForms.MessageBox.Show($"Recording could not start:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            return;
        }

        await Task.CompletedTask;
    }

    private bool ShouldIgnoreClick(int x, int y)
    {
        return ContainsMainWindowPoint(x, y);
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

        Dispatcher.BeginInvoke(async () => await HandleKeyboardInputAsync(e));
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
                var screenshot = await _screenshotService.CaptureAsync(_session.OutputDirectory, step.Index, x, y, ScreenshotCaptureMode);
                ApplyScreenshotResult(step, screenshot);
                step.ScreenshotCaptured = true;
            }
            catch (Exception ex)
            {
                step.ScreenshotCaptured = false;
                step.CaptureError = AppendError(step.CaptureError, $"Screenshot failed: {ex.Message}");
            }

            Steps.Add(step);
            RenumberSteps();
            RefreshRecordingStatus();
            UpdateSessionSummary();
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private async Task HandleKeyboardInputAsync(KeyboardInputEventArgs input)
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

        await FinalizePendingTypingAsync();
        var actionType = input.Kind == KeyboardInputKind.Shortcut ? StepActionType.Shortcut : StepActionType.SpecialKey;
        var step = CreateKeyboardActionStep(actionType, input.KeyName, input.ShortcutName);
        Steps.Add(step);
        RenumberSteps();
        RefreshRecordingStatus();
        UpdateSessionSummary();
        await RefreshKeyboardActionScreenshotAsync(step);
    }

    private void AddTextKeyToPendingStep()
    {
        var isNewTypingRun = _pendingTypingStep is null;
        var step = _pendingTypingStep ?? CreateOrUpdateTextEntryStep();
        _pendingTypingStep = step;
        if (isNewTypingRun)
        {
            _pendingTypingKeyCount = step.KeyCount ?? 0;
        }

        _pendingTypingKeyCount++;
        step.KeyboardInputDetected = true;
        step.TypedCharactersStored = false;
        step.KeyCount = step.IsSensitiveInput ? null : _pendingTypingKeyCount;

        var title = _titleGenerator.GenerateWithReason(step);
        step.GeneratedTitle = title.Title;
        step.GeneratedTitleReason = title.Reason;
        step.UserTitle = step.GeneratedTitle;

        RefreshStepViews();
        _typingTimer.Stop();
        _typingTimer.Start();
    }

    private RecordedStep CreateOrUpdateTextEntryStep()
    {
        var previous = Steps.LastOrDefault();
        if (previous is not null && previous.ActionType == StepActionType.TextEntry)
        {
            ApplyFocusedInputTarget(previous);
            return previous;
        }

        var step = CreateKeyboardActionStep(StepActionType.TextEntry, null, null);
        Steps.Add(step);
        RenumberSteps();
        RefreshRecordingStatus();
        return step;
    }

    private void ApplyFocusedInputTarget(RecordedStep step)
    {
        if (step.IsSensitiveInput)
        {
            return;
        }

        try
        {
            var focused = _uiAutomationService.GetFocusedElement();
            if (focused.IsEditable || focused.IsPassword)
            {
                step.InputTargetName = focused.UsefulElementFound ? focused.Name : step.InputTargetName;
                step.InputTargetControlType = focused.ControlType;
                step.IsSensitiveInput = focused.IsPassword;
                step.ElementName = focused.Name;
                step.AutomationId = focused.AutomationId;
                step.ControlType = focused.ControlType;
                step.ClassName = focused.ClassName;
                step.ElementBounds = focused.Bounds;
                step.UiAutomationSucceeded = focused.Quality != UiAutomationQuality.UiAutomationFailed;
                step.UiAutomationQuality = focused.Quality;
                step.UsefulElementFound = focused.UsefulElementFound;
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
            ClickX = previous?.ClickX ?? 0,
            ClickY = previous?.ClickY ?? 0,
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

    private async Task RefreshKeyboardActionScreenshotAsync(RecordedStep step)
    {
        await Task.Delay(250);
        ApplyFocusedInputTarget(step);
        await RefreshStepScreenshotAsync(step, "key", drawHighlight: false);
        RefreshStepViews();
        UpdateSessionSummary();
    }

    private async Task RefreshStepScreenshotAsync(RecordedStep step, string suffix, bool drawHighlight)
    {
        if (step.IsSensitiveInput)
        {
            return;
        }

        var captureX = step.ClickX;
        var captureY = step.ClickY;
        if (step.ElementBounds is { } bounds)
        {
            captureX = bounds.X + bounds.Width / 2;
            captureY = bounds.Y + bounds.Height / 2;
        }

        try
        {
            var screenshot = await _screenshotService.CaptureAsync(_session.OutputDirectory, $"step-{step.Index:000}-{suffix}.png", captureX, captureY, ScreenshotCaptureMode, drawHighlight);
            ApplyScreenshotResult(step, screenshot);
            step.ScreenshotCaptured = true;
        }
        catch (Exception ex)
        {
            step.ScreenshotCaptured = false;
            step.CaptureError = AppendError(step.CaptureError, $"Keyboard action screenshot failed: {ex.Message}");
        }
    }

    private async Task FinalizePendingTypingAsync()
    {
        _typingTimer.Stop();
        if (_pendingTypingStep is { } step)
        {
            await RefreshStepScreenshotAsync(step, "text", drawHighlight: false);
        }

        _pendingTypingStep = null;
        _pendingTypingKeyCount = 0;
        RefreshStepViews();
    }

    private async Task StopRecordingAsync()
    {
        _isRecording = false;
        _isPaused = false;
        await FinalizePendingTypingAsync();
        _recordingStatusTimer.Stop();
        StopHooks();

        ShowSessionEditor();
        await SaveSessionAsync(showMessage: false);
    }

    internal async Task ExportMarkdownFromEditorAsync()
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
            _session.Title = GetCurrentSessionTitle();
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

    internal async Task SaveSessionFromEditorAsync()
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
            ShowSessionEditor();
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Could not open the latest session:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    internal void DeleteStepFromEditor(object sender)
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

    internal void MoveStepUpFromEditor(object sender)
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

    internal void MoveStepDownFromEditor(object sender)
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

    internal void ViewScreenshotFromEditor(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step
            || string.IsNullOrWhiteSpace(step.EffectiveScreenshotPath)
            || !File.Exists(step.EffectiveScreenshotPath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(step.EffectiveScreenshotPath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        var preview = new Window
        {
            Title = $"Step {step.Index} Screenshot",
            Owner = _editorWindow,
            Width = Math.Min(1200, SystemParameters.WorkArea.Width * 0.9),
            Height = Math.Min(800, SystemParameters.WorkArea.Height * 0.9),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.Black,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new System.Windows.Controls.Image
                {
                    Source = image,
                    Stretch = System.Windows.Media.Stretch.None
                }
            }
        };

        preview.ShowDialog();
    }

    internal async Task EditScreenshotFromEditorAsync(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step
            || string.IsNullOrWhiteSpace(step.ScreenshotPath)
            || !File.Exists(step.ScreenshotPath))
        {
            WinForms.MessageBox.Show("No original screenshot is available for this step.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            return;
        }

        try
        {
            var editor = new ScreenshotRedactionWindow(step.EffectiveScreenshotPath ?? step.ScreenshotPath, step.Redactions)
            {
                Owner = _editorWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (editor.ShowDialog() != true)
            {
                return;
            }

            var outputPath = GetEditedScreenshotPath(step);
            step.Redactions = [.. editor.Redactions];
            if (step.Redactions.Count == 0)
            {
                step.EditedScreenshotPath = null;
            }
            else
            {
                _redactionService.ApplyRedactions(step.ScreenshotPath, outputPath, step.Redactions);
                step.EditedScreenshotPath = outputPath;
            }

            RefreshStepViews();
            await SaveSessionAsync(showMessage: false);
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Screenshot could not be edited:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    internal void SetSessionTitleFromEditor(string title)
    {
        _session.Title = title;
    }

    internal async Task StartRecordingFromEditorAsync()
    {
        _editorWindow?.Hide();
        Show();
        Activate();
        await StartRecordingAsync();
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        StopHooks();
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
        SetEditorTitle(_session.Title);
        UpdateSessionSummary();
    }

    private void LoadSession(RecordingSession session)
    {
        StopHooks();
        _isRecording = false;
        _isPaused = false;
        _session = session;
        Steps.Clear();
        foreach (var step in _session.Steps.OrderBy(step => step.Index))
        {
            Steps.Add(step);
        }

        RenumberSteps();
        SetEditorTitle(_session.Title);
        UpdateSessionSummary();
    }

    private void ShowSessionEditor()
    {
        _editorWindow ??= new SessionEditorWindow(this);
        SetEditorTitle(_session.Title);
        UpdateSessionSummary();
        _editorWindow.RefreshSteps();
        _editorWindow.Show();
        _editorWindow.Activate();
        Hide();
    }

    internal void EditorWindowClosed(SessionEditorWindow editorWindow)
    {
        if (_editorWindow == editorWindow)
        {
            _editorWindow = null;
        }

        if (!_isRecording)
        {
            Close();
        }
    }

    private string GetCurrentSessionTitle()
    {
        return _editorWindow?.SessionTitle ?? _session.Title;
    }

    private void SetEditorTitle(string title)
    {
        if (_editorWindow is not null)
        {
            _editorWindow.SessionTitle = title;
        }
    }

    private void RefreshStepViews()
    {
        _editorWindow?.RefreshSteps();
    }

    private void RenumberSteps()
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].Index = i + 1;
        }

        SyncSessionOrder();
        RefreshStepViews();
        RefreshRecordingStatus();
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

        RefreshStepViews();
        UpdateSessionSummary();
    }

    private void UpdateSessionSummary()
    {
        _editorWindow?.SetSummary($"{Steps.Count} captured step{(Steps.Count == 1 ? string.Empty : "s")} - Local session folder: {_session.OutputDirectory}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Steps)));
    }

    private void RefreshRecordingStatus()
    {
        if (StartRecordingButton is null)
        {
            return;
        }

        StartRecordingButton.Visibility = _isRecording ? Visibility.Collapsed : Visibility.Visible;
        PauseRecordingButton.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;
        StopRecordingButton.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;
        PauseRecordingButton.Content = _isPaused ? "\uE768" : "\uE769";
        PauseRecordingButton.ToolTip = _isPaused ? "Resume recording" : "Pause recording";

        if (_isRecording)
        {
            var elapsed = DateTimeOffset.Now - _recordingStartedAt;
            var state = _isPaused ? "Paused" : "Recording";
            RecordingStatusText.Text = $"{state}  {elapsed:mm\\:ss}  {Steps.Count}";
        }
        else
        {
            RecordingStatusText.Text = string.Empty;
        }
    }

    private async Task SaveSessionAsync(bool showMessage)
    {
        SyncSessionOrder();
        _session.Title = GetCurrentSessionTitle();
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

    private bool ContainsMainWindowPoint(int x, int y)
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            return false;
        }

        var point = PointFromScreen(new System.Windows.Point(x, y));
        return point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight;
    }

    private (int X, int Y) GetTestCapturePoint()
    {
        var area = WinForms.Screen.PrimaryScreen?.WorkingArea ?? WinForms.SystemInformation.WorkingArea;
        var candidates = new[]
        {
            (area.Left + area.Width / 2, area.Top + area.Height / 2),
            (area.Left + area.Width / 4, area.Top + area.Height / 2),
            (area.Left + area.Width * 3 / 4, area.Top + area.Height / 2),
            (area.Left + area.Width / 2, area.Top + area.Height / 4),
            (area.Left + area.Width / 2, area.Top + area.Height * 3 / 4)
        };

        foreach (var candidate in candidates)
        {
            if (!ContainsMainWindowPoint(candidate.Item1, candidate.Item2))
            {
                return candidate;
            }
        }

        return (area.Left + 20, area.Top + 20);
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

        return false;
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

    private static void ApplyScreenshotResult(RecordedStep step, ScreenshotCaptureResult screenshot)
    {
        step.ScreenshotPath = screenshot.FilePath;
        step.EditedScreenshotPath = null;
        step.Redactions.Clear();
        step.ScreenshotCaptureMode = screenshot.CaptureMode;
        step.LocalClickX = screenshot.LocalClickX;
        step.LocalClickY = screenshot.LocalClickY;
        step.ScreenshotWidth = screenshot.ScreenshotWidth;
        step.ScreenshotHeight = screenshot.ScreenshotHeight;
        step.MonitorDeviceName = screenshot.Monitor.DeviceName;
        step.MonitorIndex = screenshot.Monitor.Index;
        step.MonitorBoundsLeft = screenshot.Monitor.BoundsLeft;
        step.MonitorBoundsTop = screenshot.Monitor.BoundsTop;
        step.MonitorBoundsRight = screenshot.Monitor.BoundsRight;
        step.MonitorBoundsBottom = screenshot.Monitor.BoundsBottom;
        step.MonitorWidth = screenshot.Monitor.Width;
        step.MonitorHeight = screenshot.Monitor.Height;
        step.IsPrimaryMonitor = screenshot.Monitor.IsPrimary;
        step.MonitorDpiX = screenshot.Monitor.DpiX;
        step.MonitorDpiY = screenshot.Monitor.DpiY;
    }

    private static string GetEditedScreenshotPath(RecordedStep step)
    {
        var original = step.ScreenshotPath ?? $"step-{step.Index:000}.png";
        var directory = Path.GetDirectoryName(original) ?? ".";
        var name = Path.GetFileNameWithoutExtension(original);
        return Path.Combine(directory, $"{name}-redacted.png");
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
