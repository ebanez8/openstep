using System.Diagnostics;
using System.Windows;
using OpenSteps.Core.Services;

namespace OpenSteps.App;

public partial class ExportResultWindow : Window
{
    private readonly ExportResult _result;

    public ExportResultWindow(ExportResult result)
    {
        _result = result;
        InitializeComponent();
        PathText.Text = result.OutputPaths.Count == 0
            ? result.ExportFolder
            : string.Join(Environment.NewLine, result.OutputPaths);
        WarningsTextBox.Text = result.Warnings.Count == 0
            ? "No warnings."
            : string.Join(Environment.NewLine, result.Warnings);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _result.ExportFolder,
            UseShellExecute = true
        });
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_result.ExportFolder);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
