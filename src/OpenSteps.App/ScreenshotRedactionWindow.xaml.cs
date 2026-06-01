using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OpenSteps.Core.Models;
using OpenSteps.Core.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace OpenSteps.App;

public partial class ScreenshotRedactionWindow : Window
{
    private readonly List<RedactionRegion> _redactions;
    private readonly BitmapSource _imageSource;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private WpfPoint? _dragStart;
    private WpfRectangle? _previewRectangle;

    public ScreenshotRedactionWindow(string screenshotPath, IReadOnlyList<RedactionRegion> redactions)
    {
        InitializeComponent();
        _redactions = [.. redactions];

        _imageSource = LoadImage(screenshotPath);
        ScreenshotImage.Source = _imageSource;
        _imageWidth = _imageSource.PixelWidth;
        _imageHeight = _imageSource.PixelHeight;

        Loaded += (_, _) => RenderRedactions();
        SizeChanged += (_, _) => RenderRedactions();
    }

    public IReadOnlyList<RedactionRegion> Redactions => _redactions;

    private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _dragStart = e.GetPosition(OverlayCanvas);
        _previewRectangle = CreateOverlayRectangle(WpfBrushes.Transparent, WpfBrushes.White, 2);
        OverlayCanvas.Children.Add(_previewRectangle);
        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragStart is not { } start || _previewRectangle is null)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        PositionRectangle(_previewRectangle, start, current);
    }

    private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var end = e.GetPosition(OverlayCanvas);
        var region = RedactionCoordinateMapper.ToImageRegion(
            start.X,
            start.Y,
            end.X,
            end.Y,
            OverlayCanvas.ActualWidth,
            OverlayCanvas.ActualHeight,
            _imageWidth,
            _imageHeight,
            RedactionMode.Pixelate);

        if (region is not null)
        {
            _redactions.Add(region);
        }

        _dragStart = null;
        if (_previewRectangle is not null)
        {
            OverlayCanvas.Children.Remove(_previewRectangle);
            _previewRectangle = null;
        }

        OverlayCanvas.ReleaseMouseCapture();
        RenderRedactions();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_redactions.Count == 0)
        {
            return;
        }

        _redactions.RemoveAt(_redactions.Count - 1);
        RenderRedactions();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _redactions.Clear();
        RenderRedactions();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RenderRedactions()
    {
        OverlayCanvas.Children.Clear();
        foreach (var region in _redactions)
        {
            var display = ToDisplayRectangle(region);
            if (region.Mode == RedactionMode.Pixelate)
            {
                var pixelated = new System.Windows.Controls.Image
                {
                    Source = CreatePixelatedRegionSource(region),
                    Stretch = Stretch.Fill,
                    Width = display.Width,
                    Height = display.Height
                };
                RenderOptions.SetBitmapScalingMode(pixelated, BitmapScalingMode.NearestNeighbor);
                Canvas.SetLeft(pixelated, display.X);
                Canvas.SetTop(pixelated, display.Y);
                OverlayCanvas.Children.Add(pixelated);
            }
            else
            {
                var rectangle = CreateOverlayRectangle(WpfBrushes.Black, WpfBrushes.White, 1);
                Canvas.SetLeft(rectangle, display.X);
                Canvas.SetTop(rectangle, display.Y);
                rectangle.Width = display.Width;
                rectangle.Height = display.Height;
                OverlayCanvas.Children.Add(rectangle);
            }
        }
    }

    private BitmapSource CreatePixelatedRegionSource(RedactionRegion region)
    {
        var x = Math.Clamp(region.X, 0, _imageWidth);
        var y = Math.Clamp(region.Y, 0, _imageHeight);
        var width = Math.Clamp(region.Width, 0, _imageWidth - x);
        var height = Math.Clamp(region.Height, 0, _imageHeight - y);
        if (width <= 0 || height <= 0)
        {
            return BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0 }, 4);
        }

        const int blockSize = 18;
        var sourceStride = width * 4;
        var sourcePixels = new byte[sourceStride * height];
        _imageSource.CopyPixels(new Int32Rect(x, y, width, height), sourcePixels, sourceStride, 0);

        var outputPixels = new byte[sourcePixels.Length];
        for (var blockY = 0; blockY < height; blockY += blockSize)
        {
            for (var blockX = 0; blockX < width; blockX += blockSize)
            {
                FillPixelBlock(sourcePixels, outputPixels, sourceStride, width, height, blockX, blockY, blockSize);
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, outputPixels, sourceStride);
    }

    private static void FillPixelBlock(byte[] sourcePixels, byte[] outputPixels, int stride, int width, int height, int blockX, int blockY, int blockSize)
    {
        var right = Math.Min(width, blockX + blockSize);
        var bottom = Math.Min(height, blockY + blockSize);
        long blue = 0;
        long green = 0;
        long red = 0;
        long alpha = 0;
        var count = 0;

        for (var y = blockY; y < bottom; y++)
        {
            for (var x = blockX; x < right; x++)
            {
                var index = y * stride + x * 4;
                blue += sourcePixels[index];
                green += sourcePixels[index + 1];
                red += sourcePixels[index + 2];
                alpha += sourcePixels[index + 3];
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var averageBlue = (byte)(blue / count);
        var averageGreen = (byte)(green / count);
        var averageRed = (byte)(red / count);
        var averageAlpha = (byte)(alpha / count);

        for (var y = blockY; y < bottom; y++)
        {
            for (var x = blockX; x < right; x++)
            {
                var index = y * stride + x * 4;
                outputPixels[index] = averageBlue;
                outputPixels[index + 1] = averageGreen;
                outputPixels[index + 2] = averageRed;
                outputPixels[index + 3] = averageAlpha;
            }
        }
    }

    private Rect ToDisplayRectangle(RedactionRegion region)
    {
        var scale = GetImageScale();
        var displayedWidth = _imageWidth * scale;
        var displayedHeight = _imageHeight * scale;
        var offsetX = (OverlayCanvas.ActualWidth - displayedWidth) / 2;
        var offsetY = (OverlayCanvas.ActualHeight - displayedHeight) / 2;
        return new Rect(offsetX + region.X * scale, offsetY + region.Y * scale, region.Width * scale, region.Height * scale);
    }

    private double GetImageScale()
    {
        if (OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0 || _imageWidth <= 0 || _imageHeight <= 0)
        {
            return 1;
        }

        return Math.Min(OverlayCanvas.ActualWidth / _imageWidth, OverlayCanvas.ActualHeight / _imageHeight);
    }

    private static WpfRectangle CreateOverlayRectangle(WpfBrush fill, WpfBrush stroke, double strokeThickness)
    {
        return new WpfRectangle
        {
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Opacity = 0.9
        };
    }

    private static void PositionRectangle(WpfRectangle rectangle, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        rectangle.Width = Math.Abs(end.X - start.X);
        rectangle.Height = Math.Abs(end.Y - start.Y);
    }

    private static BitmapSource LoadImage(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Screenshot was not found.", path);
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
