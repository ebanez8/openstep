using System.Windows;
using System.Windows.Threading;

namespace OpenSteps.App;

public partial class RecordingToolbarWindow : Window
{
    private static System.Windows.Point? LastLocation;
    private readonly DispatcherTimer _timer;
    private bool _isClamping;
    private DateTimeOffset _startedAt;
    private int _stepCount;

    public RecordingToolbarWindow()
    {
        InitializeComponent();
        if (LastLocation is { } location)
        {
            Left = location.X;
            Top = location.Y;
        }
        else
        {
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Top + 24;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
    }

    public event EventHandler? PauseToggled;

    public event EventHandler? StopRequested;

    public bool IsPaused { get; private set; }

    public void StartClock()
    {
        _startedAt = DateTimeOffset.Now;
        _timer.Start();
        RefreshStatus();
    }

    public void SetStepCount(int count)
    {
        _stepCount = count;
        RefreshStatus();
    }

    public bool ContainsScreenPoint(int x, int y)
    {
        var point = PointFromScreen(new System.Windows.Point(x, y));
        return point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        IsPaused = !IsPaused;
        PauseButton.Content = IsPaused ? "Resume" : "Pause";
        RefreshStatus();
        PauseToggled?.Invoke(this, EventArgs.Empty);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
        {
            return;
        }

        try
        {
            DragMove();
            ClampToWorkArea();
        }
        catch
        {
            ClampToWorkArea();
        }
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        ClampToWorkArea();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isClamping)
        {
            return;
        }

        ClampToWorkArea();
        LastLocation = new System.Windows.Point(Left, Top);
    }

    private void RefreshStatus()
    {
        var elapsed = DateTimeOffset.Now - _startedAt;
        var state = IsPaused ? "Paused" : "Recording";
        StatusText.Text = $"{state}  {elapsed:mm\\:ss}  {_stepCount} steps";
    }

    private void ClampToWorkArea()
    {
        _isClamping = true;
        try
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Min(Math.Max(Left, area.Left), area.Right - ActualWidth);
            Top = Math.Min(Math.Max(Top, area.Top), area.Bottom - ActualHeight);
            LastLocation = new System.Windows.Point(Left, Top);
        }
        finally
        {
            _isClamping = false;
        }
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
