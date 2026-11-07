using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OpenSteps.App;

/// <summary>
/// Small "REC" badge in the top-right corner while recording. It ignores the mouse, never takes
/// focus, and is excluded from screen capture so it does not appear in step screenshots.
/// </summary>
public partial class RecordingIndicatorWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const uint WdaExcludeFromCapture = 0x11;

    private static readonly SolidColorBrush RecordingBrush = new(System.Windows.Media.Color.FromRgb(0xE0, 0x39, 0x4A));
    private static readonly SolidColorBrush PausedBrush = new(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));

    public RecordingIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MakeOverlay();
        SizeChanged += (_, _) => PositionTopRight();
    }

    public void Update(bool paused, TimeSpan elapsed, int stepCount, string? stopHotkey)
    {
        StateText.Text = paused ? "Paused" : "REC";
        Dot.Fill = paused ? PausedBrush : RecordingBrush;
        DetailText.Text = $"{elapsed:mm\\:ss} · {stepCount} {(stepCount == 1 ? "step" : "steps")}";
        HintText.Text = stopHotkey is null ? string.Empty : $"{stopHotkey} to stop";
        HintText.Visibility = stopHotkey is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PositionTopRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 6;
        Top = area.Top + 6;
    }

    private void MakeOverlay()
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExTransparent | WsExToolWindow | WsExNoActivate);
        SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
}
