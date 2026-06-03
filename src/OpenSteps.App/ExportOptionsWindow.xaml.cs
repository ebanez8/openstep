using System.Windows;
using OpenSteps.Core.Services;

namespace OpenSteps.App;

public partial class ExportOptionsWindow : Window
{
    public ExportOptionsWindow()
    {
        InitializeComponent();
    }

    public GuideExportFormat SelectedFormats { get; private set; } = GuideExportFormat.Markdown | GuideExportFormat.Html;

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        var formats = 0;
        if (MarkdownCheckBox.IsChecked == true)
        {
            formats |= (int)GuideExportFormat.Markdown;
        }

        if (HtmlCheckBox.IsChecked == true)
        {
            formats |= (int)GuideExportFormat.Html;
        }

        if (formats == 0)
        {
            System.Windows.MessageBox.Show("Choose at least one export file type.", "OpenSteps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedFormats = (GuideExportFormat)formats;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
