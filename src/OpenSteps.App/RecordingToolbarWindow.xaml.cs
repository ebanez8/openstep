using System.Windows;
using System.Windows.Threading;

namespace OpenSteps.App;

public partial class RecordingToolbarWindow : Window
{
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _startedAt;
    private int _stepCount;

    public RecordingToolbarWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
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

    private void RefreshStatus()
    {
        var elapsed = DateTimeOffset.Now - _startedAt;
        var state = IsPaused ? "Paused" : "Recording";
        StatusText.Text = $"{state}  {elapsed:mm\\:ss}  {_stepCount} steps";
    }
}
