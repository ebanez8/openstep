using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using OpenSteps.Core.Models;
using WinForms = System.Windows.Forms;

namespace OpenSteps.App;

public partial class SessionPickerWindow : Window
{
    private readonly MainWindow _controller;

    public SessionPickerWindow(MainWindow controller)
    {
        _controller = controller;
        Sessions = [];
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await RefreshSessionsAsync();
        Closed += (_, _) => _controller.SessionPickerClosed(this);
    }

    public ObservableCollection<SessionSummary> Sessions { get; }

    public async Task RefreshSessionsAsync()
    {
        Sessions.Clear();
        foreach (var session in await _controller.ListSavedSessionsAsync())
        {
            Sessions.Add(session);
        }

        EmptyText.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_controller.SessionsRootDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _controller.SessionsRootDirectory,
            UseShellExecute = true
        });
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SessionSummary summary)
        {
            return;
        }

        await _controller.OpenSavedSessionAsync(summary.Id);
        Close();
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SessionSummary summary)
        {
            return;
        }

        await _controller.RenameSavedSessionAsync(summary.Id, summary.Title);
        await RefreshSessionsAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SessionSummary summary)
        {
            return;
        }

        var result = WinForms.MessageBox.Show(
            $"Delete \"{summary.Title}\"?\n\nThis removes the saved session folder from this computer.",
            "OpenSteps",
            WinForms.MessageBoxButtons.YesNo,
            WinForms.MessageBoxIcon.Warning);
        if (result != WinForms.DialogResult.Yes)
        {
            return;
        }

        await _controller.DeleteSavedSessionAsync(summary.Id);
        await RefreshSessionsAsync();
    }
}
