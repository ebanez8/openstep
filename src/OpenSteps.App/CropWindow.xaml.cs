using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WinForms = System.Windows.Forms;

namespace OpenSteps.App;

public partial class CropWindow : Window
{
    private readonly BitmapImage _bitmap;
    private WpfPoint? _startPoint;
    private Rect? _selection;

    public CropWindow(string screenshotPath)
    {
        InitializeComponent();
        _bitmap = LoadBitmap(screenshotPath);
        ScreenshotImage.Source = _bitmap;
    }

    public Int32Rect CropRect { get; private set; }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var imageRect = GetRenderedImageRect();
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return;
        }

        _startPoint = ClampPoint(e.GetPosition(OverlayCanvas), imageRect);
        OverlayCanvas.CaptureMouse();
        UpdateSelection(_startPoint.Value, _startPoint.Value);
    }

    private void OverlayCanvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_startPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelection(_startPoint.Value, ClampPoint(e.GetPosition(OverlayCanvas), GetRenderedImageRect()));
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint is null)
        {
            return;
        }

        UpdateSelection(_startPoint.Value, ClampPoint(e.GetPosition(OverlayCanvas), GetRenderedImageRect()));
        _startPoint = null;
        OverlayCanvas.ReleaseMouseCapture();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selection is not { Width: > 1, Height: > 1 } selection)
        {
            WinForms.MessageBox.Show("Select an area to crop first.", "OpenSteps", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            return;
        }

        var imageRect = GetRenderedImageRect();
        var scaleX = _bitmap.PixelWidth / imageRect.Width;
        var scaleY = _bitmap.PixelHeight / imageRect.Height;

        var x = (int)Math.Floor((selection.X - imageRect.X) * scaleX);
        var y = (int)Math.Floor((selection.Y - imageRect.Y) * scaleY);
        var width = (int)Math.Ceiling(selection.Width * scaleX);
        var height = (int)Math.Ceiling(selection.Height * scaleY);

        x = Math.Clamp(x, 0, _bitmap.PixelWidth - 1);
        y = Math.Clamp(y, 0, _bitmap.PixelHeight - 1);
        width = Math.Clamp(width, 1, _bitmap.PixelWidth - x);
        height = Math.Clamp(height, 1, _bitmap.PixelHeight - y);

        CropRect = new Int32Rect(x, y, width, height);
        DialogResult = true;
        Close();
    }

    private void ResetSelection_Click(object sender, RoutedEventArgs e)
    {
        _selection = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateSelection(WpfPoint start, WpfPoint end)
    {
        var imageRect = GetRenderedImageRect();
        var selection = new Rect(start, end);
        selection.Intersect(imageRect);
        _selection = selection;

        Canvas.SetLeft(SelectionRectangle, selection.X);
        Canvas.SetTop(SelectionRectangle, selection.Y);
        SelectionRectangle.Width = selection.Width;
        SelectionRectangle.Height = selection.Height;
        SelectionRectangle.Visibility = selection.Width > 0 && selection.Height > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Rect GetRenderedImageRect()
    {
        var hostWidth = OverlayCanvas.ActualWidth;
        var hostHeight = OverlayCanvas.ActualHeight;
        if (hostWidth <= 0 || hostHeight <= 0 || _bitmap.PixelWidth <= 0 || _bitmap.PixelHeight <= 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(hostWidth / _bitmap.PixelWidth, hostHeight / _bitmap.PixelHeight);
        var width = _bitmap.PixelWidth * scale;
        var height = _bitmap.PixelHeight * scale;
        return new Rect((hostWidth - width) / 2, (hostHeight - height) / 2, width, height);
    }

    private static WpfPoint ClampPoint(WpfPoint point, Rect bounds)
    {
        return new WpfPoint(
            Math.Clamp(point.X, bounds.Left, bounds.Right),
            Math.Clamp(point.Y, bounds.Top, bounds.Bottom));
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
