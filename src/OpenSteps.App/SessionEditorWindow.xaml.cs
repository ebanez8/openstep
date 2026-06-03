using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OpenSteps.App;

public partial class SessionEditorWindow : Window
{
    private readonly MainWindow _controller;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _updatingTitle;

    public SessionEditorWindow(MainWindow controller)
    {
        _controller = controller;
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _autosaveTimer.Tick += async (_, _) =>
        {
            _autosaveTimer.Stop();
            await _controller.AutosaveSessionFromEditorAsync();
        };
        InitializeComponent();
        DataContext = controller;
        Closing += SessionEditorWindow_Closing;
    }

    public string SessionTitle
    {
        get => SessionTitleBox.Text;
        set
        {
            _updatingTitle = true;
            SessionTitleBox.Text = value;
            _updatingTitle = false;
        }
    }

    public void SetSummary(string summary)
    {
    }

    public void RefreshSteps()
    {
        StepsList.Items.Refresh();
        var hasSteps = _controller.Steps.Count > 0;
        EmptyState.Visibility = hasSteps ? Visibility.Collapsed : Visibility.Visible;
        StepsList.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BackToRecorder_Click(object sender, RoutedEventArgs e)
    {
        await _controller.ReturnToRecorderFromEditorAsync();
    }

    private async void RecordMore_Click(object sender, RoutedEventArgs e)
    {
        await _controller.StartRecordingFromEditorAsync();
    }

    private async void SaveSession_Click(object sender, RoutedEventArgs e)
    {
        await _controller.SaveSessionFromEditorAsync();
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        await _controller.ExportMarkdownFromEditorAsync();
    }

    private void ViewScreenshot_Click(object sender, RoutedEventArgs e)
    {
        _controller.ViewScreenshotFromEditor(sender);
    }

    private async void EditScreenshot_Click(object sender, RoutedEventArgs e)
    {
        await _controller.EditScreenshotFromEditorAsync(sender);
    }

    private async void CaptureStepScreenshot_Click(object sender, RoutedEventArgs e)
    {
        await _controller.CaptureScreenshotForStepFromEditorAsync(sender);
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        _controller.MoveStepUpFromEditor(sender);
    }

    private async void InsertManualStepBelow_Click(object sender, RoutedEventArgs e)
    {
        await _controller.InsertManualStepBelowFromEditorAsync(sender);
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        _controller.MoveStepDownFromEditor(sender);
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        _controller.DeleteStepFromEditor(sender);
    }

    private void SessionTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingTitle)
        {
            _controller.SetSessionTitleFromEditor(SessionTitleBox.Text);
        }
    }

    private void StepTextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ScheduleAutosave();
        }
    }

    private void ScheduleAutosave()
    {
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private void SessionEditorWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autosaveTimer.Stop();
        _controller.EditorWindowClosed(this);
    }
}
