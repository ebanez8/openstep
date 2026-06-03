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

public sealed record ScreenshotModeOption(string DisplayName, ScreenshotMode Mode);

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int ActivationSettleDelayMs = 150;

    private readonly ActiveWindowService _activeWindowService = new();
    private readonly ClickTargetClassifier _clickTargetClassifier = new();
    private readonly CaptureTargetResolver _captureTargetResolver = new();
    private readonly MonitorService _monitorService = new();
    private readonly ScreenshotService _screenshotService;
    private readonly UiAutomationService _uiAutomationService = new();
    private readonly DpiAwarenessService _dpiAwarenessService = new();
    private readonly ScreenshotRedactionService _redactionService = new();
    private readonly StepTitleGenerator _titleGenerator = new();
    private readonly MarkdownBuilder _markdownBuilder = new();
    private readonly MarkdownExporter _markdownExporter = new();
    private readonly SessionStore _sessionStore = new();
    private readonly SettingsService _settingsService = new();
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly DispatcherTimer _typingTimer;
    private readonly DispatcherTimer _recordingStatusTimer;

    private RecordingSession _session = new();
    private GlobalMouseHook? _mouseHook;
    private GlobalKeyboardHook? _keyboardHook;
    private RecordedStep? _pendingTypingStep;
    private RecordedStep? _screenshotCaptureTargetStep;
    private SessionEditorWindow? _editorWindow;
    private SessionPickerWindow? _sessionPickerWindow;
    private int _pendingTypingKeyCount;
    private bool _isRecording;
    private bool _isPaused;
    private DateTimeOffset _recordingStartedAt;
    private AppSettings _settings = new();
    private ScreenshotMode _screenshotMode = ScreenshotMode.FullDesktop;
    private bool _loadingSettings;

    public MainWindow()
    {
        _screenshotService = new ScreenshotService(_monitorService);
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) =>
        {
            PositionControllerBottomCenter();
            await LoadSettingsAsync();
        };
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

    public ScreenshotModeOption[] ScreenshotModeOptions { get; } =
    [
        new("Full desktop", ScreenshotMode.FullDesktop),
        new("Active window only", ScreenshotMode.ActiveWindow)
    ];

    public ScreenshotMode ScreenshotMode
    {
        get => _screenshotMode;
        set
        {
            if (_screenshotMode == value)
            {
                return;
            }

            _screenshotMode = value;
            _settings.ScreenshotMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScreenshotMode)));
            if (!_loadingSettings)
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    internal string SessionsRootDirectory => _sessionStore.RootDirectory;

    private async Task LoadSettingsAsync()
    {
        _loadingSettings = true;
        try
        {
            _settings = await _settingsService.LoadAsync();
            ScreenshotMode = _settings.ScreenshotMode;
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch
        {
            // Settings persistence should not block recording.
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
        await LoadSettingsAsync();
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
        return false;
    }

    private void MouseHook_ClickCaptured(object? sender, ClickCapturedEventArgs e)
    {
        if (!_isRecording || _isPaused)
        {
            return;
        }

        Dispatcher.BeginInvoke(async () => await HandleCapturedClickAsync(e.X, e.Y));
    }

    private void KeyboardHook_KeyboardInputCaptured(object? sender, KeyboardInputEventArgs e)
    {
        if (!_isRecording || _isPaused || _screenshotCaptureTargetStep is not null)
        {
            return;
        }

        Dispatcher.BeginInvoke(async () => await HandleKeyboardInputAsync(e));
    }

    private async Task HandleCapturedClickAsync(int x, int y)
    {
        var openStepsHandles = GetOpenStepsWindowHandles();
        var immediateTarget = _clickTargetClassifier.ClassifyPoint(x, y, openStepsHandles);

        if (immediateTarget.Classification is ClickClassification.OpenStepsWindow or ClickClassification.TaskbarOrShell)
        {
            return;
        }

        await Task.Delay(ActivationSettleDelayMs);

        openStepsHandles = GetOpenStepsWindowHandles();
        var foregroundAfterDelay = _activeWindowService.GetForegroundWindowHandle();
        var foregroundTarget = _clickTargetClassifier.ClassifyWindow(foregroundAfterDelay, openStepsHandles);
        var resolution = _captureTargetResolver.Resolve(immediateTarget, foregroundTarget);
        if (!resolution.ShouldRecord)
        {
            return;
        }

        if (_screenshotCaptureTargetStep is { } targetStep)
        {
            await CaptureScreenshotForExistingStepAsync(targetStep, x, y, resolution);
            return;
        }

        await CaptureStepAsync(x, y, resolution);
    }

    private async Task CaptureStepAsync(int x, int y, CaptureTargetResolution? resolution = null)
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
                ApplyActiveWindowMetadata(step, resolution?.TargetHwnd ?? IntPtr.Zero, x, y);
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
                var screenshot = await _screenshotService.CaptureAsync(GetSessionImagesDirectory(), step.Index, x, y, ScreenshotMode, resolution?.TargetHwnd ?? IntPtr.Zero);
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
            var screenshot = await _screenshotService.CaptureAsync(GetSessionImagesDirectory(), $"step-{step.Index:000}-{suffix}.png", captureX, captureY, ScreenshotMode, drawHighlight);
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
        var options = new ExportOptionsWindow
        {
            Owner = _editorWindow
        };

        if (options.ShowDialog() != true)
        {
            return;
        }

        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose where to create the exported guide folder",
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
            var result = await _markdownExporter.ExportAsync(_session, dialog.SelectedPath, options.SelectedFormats);
            new ExportResultWindow(result)
            {
                Owner = _editorWindow
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Export failed:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    internal async Task PreviewMarkdownFromEditorAsync()
    {
        SyncSessionOrder();
        _session.Title = GetCurrentSessionTitle();
        await SaveSessionAsync(showMessage: false);
        new MarkdownPreviewWindow(_markdownBuilder.BuildMarkdown(_session), _session)
        {
            Owner = _editorWindow
        }.ShowDialog();
    }

    internal async Task SaveSessionFromEditorAsync()
    {
        await SaveSessionAsync(showMessage: true);
    }

    internal async Task AutosaveSessionFromEditorAsync()
    {
        await SaveSessionAsync(showMessage: false);
    }

    private async void OpenPreviousSession_Click(object sender, RoutedEventArgs e)
    {
        await ShowSessionPickerAsync();
    }

    private async Task ShowSessionPickerAsync()
    {
        try
        {
            _sessionPickerWindow ??= new SessionPickerWindow(this);
            await _sessionPickerWindow.RefreshSessionsAsync();
            _sessionPickerWindow.Show();
            _sessionPickerWindow.Activate();
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Could not open saved sessions:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    internal async Task<IReadOnlyList<SessionSummary>> ListSavedSessionsAsync()
    {
        return await _sessionStore.ListSessionsAsync();
    }

    internal async Task OpenSavedSessionAsync(Guid sessionId)
    {
        var session = await _sessionStore.LoadSessionAsync(sessionId);
        if (session is null)
        {
            WinForms.MessageBox.Show("That saved session could not be opened. It may have been deleted or its session.json may be damaged.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            return;
        }

        LoadSession(session);
        ShowSessionEditor();
    }

    internal async Task RenameSavedSessionAsync(Guid sessionId, string title)
    {
        await _sessionStore.RenameSessionAsync(sessionId, title);
    }

    internal async Task DeleteSavedSessionAsync(Guid sessionId)
    {
        await _sessionStore.DeleteSessionAsync(sessionId);
        if (_session.Id == sessionId)
        {
            StopHooks();
            _isRecording = false;
            _isPaused = false;
            _recordingStatusTimer.Stop();
            _typingTimer.Stop();
            _pendingTypingStep = null;
            _pendingTypingKeyCount = 0;
            ResetSession();
            RefreshRecordingStatus();
        }
    }

    internal void SessionPickerClosed(SessionPickerWindow pickerWindow)
    {
        if (_sessionPickerWindow == pickerWindow)
        {
            _sessionPickerWindow = null;
        }
    }

    internal async Task AddManualStepFromEditorAsync()
    {
        StepEditorOperations.AddManualStepAtEnd(Steps);
        SyncSessionOrder();
        await SaveSessionAsync(showMessage: false);
    }

    internal async Task InsertManualStepBelowFromEditorAsync(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step)
        {
            return;
        }

        StepEditorOperations.InsertManualStepBelow(Steps, step);
        SyncSessionOrder();
        await SaveSessionAsync(showMessage: false);
    }

    internal async Task CaptureScreenshotForStepFromEditorAsync(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecordedStep step)
        {
            return;
        }

        _screenshotCaptureTargetStep = step;
        _editorWindow?.Hide();
        Show();
        Activate();
        await StartRecordingAsync();
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

            if (editor.CropRequested)
            {
                await CropScreenshotForStepAsync(step);
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

    private async Task CropScreenshotForStepAsync(RecordedStep step)
    {
        if (string.IsNullOrWhiteSpace(step.EffectiveScreenshotPath)
            || !File.Exists(step.EffectiveScreenshotPath))
        {
            WinForms.MessageBox.Show("No screenshot is available to crop for this step.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            return;
        }

        try
        {
            var cropWindow = new CropWindow(step.EffectiveScreenshotPath)
            {
                Owner = _editorWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (cropWindow.ShowDialog() != true)
            {
                return;
            }

            var source = LoadBitmapFrame(step.EffectiveScreenshotPath);
            var cropRect = cropWindow.CropRect;
            if (cropRect.Width <= 0 || cropRect.Height <= 0)
            {
                WinForms.MessageBox.Show("The selected crop area was empty.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            var cropped = new CroppedBitmap(source, cropRect);
            var outputPath = GetCroppedScreenshotPath(step);
            using (var stream = File.Create(outputPath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(cropped));
                encoder.Save(stream);
            }

            step.ScreenshotPath = outputPath;
            step.ScreenshotRelativePath = Path.Combine("images", Path.GetFileName(outputPath)).Replace('\\', '/');
            step.EditedScreenshotPath = null;
            step.EditedScreenshotRelativePath = null;
            step.Redactions.Clear();
            step.ScreenshotCaptured = true;
            step.ScreenshotWidth = cropped.PixelWidth;
            step.ScreenshotHeight = cropped.PixelHeight;

            RefreshStepViews();
            await SaveSessionAsync(showMessage: false);
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Screenshot could not be cropped:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    private async Task CaptureScreenshotForExistingStepAsync(RecordedStep step, int x, int y, CaptureTargetResolution resolution)
    {
        try
        {
            _screenshotCaptureTargetStep = null;
            _isRecording = false;
            _isPaused = false;
            _recordingStatusTimer.Stop();
            StopHooks();

            step.ClickX = x;
            step.ClickY = y;
            step.VirtualScreenBounds = GetVirtualScreenBounds();
            step.ProcessDpiAwareness = _dpiAwarenessService.GetCurrentThreadAwareness();

            try
            {
                ApplyActiveWindowMetadata(step, resolution.TargetHwnd, x, y);
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
            if (string.IsNullOrWhiteSpace(step.UserTitle) || step.UserTitle == "New step" || step.UserTitle == "Manual step")
            {
                step.UserTitle = title.Title;
            }

            var screenshot = await _screenshotService.CaptureAsync(GetSessionImagesDirectory(), $"step-{step.Index:000}-manual.png", x, y, ScreenshotMode, resolution.TargetHwnd);
            ApplyScreenshotResult(step, screenshot);
            step.ScreenshotCaptured = true;
            SyncSessionOrder();
            await SaveSessionAsync(showMessage: false);
        }
        catch (Exception ex)
        {
            step.ScreenshotCaptured = false;
            step.CaptureError = AppendError(step.CaptureError, $"Manual screenshot failed: {ex.Message}");
        }
        finally
        {
            _screenshotCaptureTargetStep = null;
            RefreshRecordingStatus();
            ShowSessionEditor();
        }
    }

    internal void SetSessionTitleFromEditor(string title)
    {
        _session.Title = title;
    }

    internal async Task ReturnToRecorderFromEditorAsync()
    {
        await SaveSessionAsync(showMessage: false);
        _editorWindow?.Hide();
        ResetSession();
        Show();
        Activate();
        RefreshRecordingStatus();
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
        var sessionDirectory = string.IsNullOrWhiteSpace(_session.SessionDirectory)
            ? _session.OutputDirectory
            : _session.SessionDirectory;
        if (!string.IsNullOrWhiteSpace(sessionDirectory) && Directory.Exists(sessionDirectory))
        {
            return;
        }

        ResetSession();
    }

    private void ResetSession()
    {
        var sessionId = Guid.NewGuid();
        var sessionDirectory = Path.Combine(_sessionStore.RootDirectory, sessionId.ToString("N"));
        _session = new RecordingSession
        {
            Id = sessionId,
            Title = $"OpenSteps Guide {DateTimeOffset.Now:yyyy-MM-dd HH.mm}",
            SessionDirectory = sessionDirectory,
            OutputDirectory = sessionDirectory
        };

        Directory.CreateDirectory(GetSessionImagesDirectory());
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

    private string GetSessionImagesDirectory()
    {
        var sessionDirectory = string.IsNullOrWhiteSpace(_session.SessionDirectory)
            ? _session.OutputDirectory
            : _session.SessionDirectory;
        var imagesDirectory = Path.Combine(sessionDirectory, "images");
        Directory.CreateDirectory(imagesDirectory);
        return imagesDirectory;
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
            var stepLabel = Steps.Count == 1 ? "step" : "steps";
            RecordingStatusText.Text = $"{state}  ·  {elapsed:mm\\:ss}  ·  {Steps.Count} {stepLabel}";
            RecordingDot.Visibility = _isPaused ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            RecordingStatusText.Text = string.Empty;
            RecordingDot.Visibility = Visibility.Collapsed;
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

    private IReadOnlySet<IntPtr> GetOpenStepsWindowHandles()
    {
        var handles = new HashSet<IntPtr>();
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                handles.Add(handle);
            }
        }

        return handles;
    }

    private void ApplyActiveWindowMetadata(RecordedStep step, IntPtr targetHwnd, int x, int y)
    {
        var activeWindow = targetHwnd == IntPtr.Zero
            ? _activeWindowService.Capture()
            : _activeWindowService.Capture(targetHwnd);
        step.ActiveWindowHandle = activeWindow.Handle;
        step.WindowTitle = activeWindow.Title;
        step.ProcessName = activeWindow.ProcessName;
        step.ExecutablePath = activeWindow.ExecutablePath;
        step.WindowBounds = activeWindow.Bounds;
        step.ClickInsideActiveWindowBounds = ContainsBounds(activeWindow.Bounds, x, y);
    }

    private void PositionControllerBottomCenter()
    {
        var area = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        Left = area.Left + Math.Max(0, (area.Width - width) / 2);
        Top = area.Bottom - height - 24;
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
        step.RequestedScreenshotMode = screenshot.RequestedScreenshotMode;
        step.ActualScreenshotMode = screenshot.ActualScreenshotMode;
        step.UsedScreenshotFallback = screenshot.UsedScreenshotFallback;
        step.ScreenshotError = screenshot.ScreenshotError;
        step.CapturedBoundsLeft = screenshot.CapturedBounds?.X;
        step.CapturedBoundsTop = screenshot.CapturedBounds?.Y;
        step.CapturedBoundsRight = screenshot.CapturedBounds is { } capturedBounds ? capturedBounds.X + capturedBounds.Width : null;
        step.CapturedBoundsBottom = screenshot.CapturedBounds is { } capturedBounds2 ? capturedBounds2.Y + capturedBounds2.Height : null;
        step.BoundsSource = screenshot.BoundsSource;
        step.GlobalClickX = screenshot.GlobalClickX;
        step.GlobalClickY = screenshot.GlobalClickY;
        step.HighlightX = screenshot.HighlightX;
        step.HighlightY = screenshot.HighlightY;
        step.HighlightWasInsideCapturedBounds = screenshot.HighlightWasInsideCapturedBounds;
        step.LocalClickX = screenshot.LocalClickX;
        step.LocalClickY = screenshot.LocalClickY;
        step.ScreenshotWidth = screenshot.ScreenshotWidth;
        step.ScreenshotHeight = screenshot.ScreenshotHeight;
        step.MonitorDeviceName = screenshot.Monitor?.DeviceName;
        step.MonitorIndex = screenshot.Monitor?.Index;
        step.MonitorBoundsLeft = screenshot.Monitor?.BoundsLeft;
        step.MonitorBoundsTop = screenshot.Monitor?.BoundsTop;
        step.MonitorBoundsRight = screenshot.Monitor?.BoundsRight;
        step.MonitorBoundsBottom = screenshot.Monitor?.BoundsBottom;
        step.MonitorWidth = screenshot.Monitor?.Width;
        step.MonitorHeight = screenshot.Monitor?.Height;
        step.IsPrimaryMonitor = screenshot.Monitor?.IsPrimary;
        step.MonitorDpiX = screenshot.Monitor?.DpiX;
        step.MonitorDpiY = screenshot.Monitor?.DpiY;
    }

    private static string GetEditedScreenshotPath(RecordedStep step)
    {
        var original = step.ScreenshotPath ?? $"step-{step.Index:000}.png";
        var directory = Path.GetDirectoryName(original) ?? ".";
        var name = Path.GetFileNameWithoutExtension(original);
        return Path.Combine(directory, $"{name}-redacted.png");
    }

    private string GetCroppedScreenshotPath(RecordedStep step)
    {
        var imagesDirectory = GetSessionImagesDirectory();
        var baseName = $"step-{step.Index:000}";
        return Path.Combine(imagesDirectory, $"{baseName}-crop-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}.png");
    }

    private static BitmapFrame LoadBitmapFrame(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return BitmapFrame.Create(bitmap);
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
