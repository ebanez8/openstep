using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace OpenSteps.App;

public partial class SessionEditorWindow : Window
{
    private readonly MainWindow _controller;
    private bool _updatingTitle;

    public SessionEditorWindow(MainWindow controller)
    {
        _controller = controller;
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
        SessionSummaryText.Text = summary;
    }

    public void RefreshSteps()
    {
        StepsList.Items.Refresh();
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

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        _controller.MoveStepUpFromEditor(sender);
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

    private void SessionEditorWindow_Closing(object? sender, CancelEventArgs e)
    {
        _controller.EditorWindowClosed(this);
    }
}
