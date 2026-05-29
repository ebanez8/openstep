using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private readonly StepTitleGenerator _titleGenerator = new();
    private readonly MarkdownExporter _markdownExporter = new();
    private readonly SemaphoreSlim _captureLock = new(1, 1);

    private RecordingSession _session = new();
    private GlobalMouseHook? _mouseHook;
    private RecordingToolbarWindow? _toolbar;
    private bool _isRecording;
    private bool _isPaused;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        ResetSession();
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
        _isRecording = true;
        _isPaused = false;

        _toolbar?.Close();
        _toolbar = new RecordingToolbarWindow();
        _toolbar.PauseToggled += (_, _) => _isPaused = _toolbar.IsPaused;
        _toolbar.StopRequested += (_, _) => StopRecording();
        _toolbar.SetStepCount(Steps.Count);
        _toolbar.Show();
        _toolbar.StartClock();

        _mouseHook?.Dispose();
        _mouseHook = new GlobalMouseHook(ShouldIgnoreClick);
        _mouseHook.ClickCaptured += MouseHook_ClickCaptured;
        _mouseHook.Start();

        WindowState = WindowState.Minimized;
        await Task.CompletedTask;
    }

    private bool ShouldIgnoreClick(int x, int y)
    {
        return _toolbar?.ContainsScreenPoint(x, y) == true;
    }

    private void MouseHook_ClickCaptured(object? sender, ClickCapturedEventArgs e)
    {
        if (!_isRecording || _isPaused)
        {
            return;
        }

        Dispatcher.BeginInvoke(async () => await CaptureStepAsync(e.X, e.Y));
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
                ClickY = y
            };

            try
            {
                var activeWindow = _activeWindowService.Capture();
                step.ActiveWindowHandle = activeWindow.Handle;
                step.WindowTitle = activeWindow.Title;
                step.ProcessName = activeWindow.ProcessName;
                step.ExecutablePath = activeWindow.ExecutablePath;
                step.WindowBounds = activeWindow.Bounds;
            }
            catch (Exception ex)
            {
                step.CaptureError = AppendError(step.CaptureError, $"Window metadata failed: {ex.Message}");
            }

            try
            {
                var element = _uiAutomationService.GetElementAt(x, y);
                if (element is not null)
                {
                    step.ElementName = element.Name;
                    step.AutomationId = element.AutomationId;
                    step.ControlType = element.ControlType;
                    step.ClassName = element.ClassName;
                    step.ElementBounds = element.Bounds;
                    step.ParentElementName = element.ParentName;
                }
            }
            catch (Exception ex)
            {
                step.CaptureError = AppendError(step.CaptureError, $"UI Automation failed: {ex.Message}");
            }

            step.GeneratedTitle = _titleGenerator.Generate(step);
            step.UserTitle = step.GeneratedTitle;

            try
            {
                step.ScreenshotPath = await _screenshotService.CaptureVirtualDesktopAsync(_session.OutputDirectory, step.Index, x, y);
            }
            catch (Exception ex)
            {
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

    private void StopRecording()
    {
        _isRecording = false;
        _isPaused = false;

        if (_mouseHook is not null)
        {
            _mouseHook.ClickCaptured -= MouseHook_ClickCaptured;
            _mouseHook.Dispose();
            _mouseHook = null;
        }

        _toolbar?.Close();
        _toolbar = null;

        WindowState = WindowState.Normal;
        Show();
        Activate();
        ShowEditor();
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
            _session.Title = SessionTitleBox.Text;
            var path = await _markdownExporter.ExportAsync(_session, dialog.SelectedPath);
            WinForms.MessageBox.Show($"Exported guide:\n{path}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show($"Export failed:\n{ex.Message}", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
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
        }
    }

    private void SessionTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_session is not null)
        {
            _session.Title = SessionTitleBox.Text;
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
        SessionSummaryText.Text = $"{Steps.Count} captured step{(Steps.Count == 1 ? string.Empty : "s")} · Local session folder: {_session.OutputDirectory}";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Steps)));
    }

    private static string AppendError(string? existing, string next)
    {
        return string.IsNullOrWhiteSpace(existing) ? next : $"{existing}; {next}";
    }
}
