using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using OpenSteps.Core.Models;
using WinForms = System.Windows.Forms;

namespace OpenSteps.App;

public partial class SessionPickerWindow : Window, INotifyPropertyChanged
{
    private readonly MainWindow _controller;
    private int _sessionGridColumns = 1;

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

    public int SessionGridColumns
    {
        get => _sessionGridColumns;
        private set
        {
            if (_sessionGridColumns == value)
            {
                return;
            }

            _sessionGridColumns = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshSessionsAsync()
    {
        Sessions.Clear();
        foreach (var session in await _controller.ListSavedSessionsAsync())
        {
            Sessions.Add(session);
        }

        SessionGridColumns = Math.Clamp(Sessions.Count, 1, 3);
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
