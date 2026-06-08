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
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace OpenSteps.App;

public partial class ScreenshotRedactionWindow : Window
{
    private readonly List<RedactionRegion> _redactions;
    private readonly List<ScreenshotAnnotation> _annotations = [];
    private readonly BitmapSource _imageSource;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private WpfPoint? _dragStart;
    private Shape? _previewShape;
    private ScreenshotEditTool _selectedTool = ScreenshotEditTool.Pixelate;
    private int _nextMarkerNumber = 1;
    private char _nextMarkerLetter = 'A';

    public ScreenshotRedactionWindow(string screenshotPath, IReadOnlyList<RedactionRegion> redactions)
    {
        InitializeComponent();
        _redactions = [.. redactions];

        _imageSource = LoadImage(screenshotPath);
        ScreenshotImage.Source = _imageSource;
        _imageWidth = _imageSource.PixelWidth;
        _imageHeight = _imageSource.PixelHeight;

        Loaded += (_, _) => RenderEdits();
        SizeChanged += (_, _) => RenderEdits();
    }

    public IReadOnlyList<RedactionRegion> Redactions => _redactions;

    public IReadOnlyList<ScreenshotAnnotation> Annotations => _annotations;

    public bool HasChanges { get; private set; }

    public bool CropRequested { get; private set; }

    private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var start = e.GetPosition(OverlayCanvas);
        if (_selectedTool is ScreenshotEditTool.NumberMarker or ScreenshotEditTool.LetterMarker)
        {
            AddMarker(start);
            return;
        }

        _dragStart = start;
        _previewShape = CreatePreviewShape();
        OverlayCanvas.Children.Add(_previewShape);
        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragStart is not { } start || _previewShape is null)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        PositionPreviewShape(_previewShape, start, current);
    }

    private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var end = e.GetPosition(OverlayCanvas);
        AddDragEdit(start, end);

        _dragStart = null;
        if (_previewShape is not null)
        {
            OverlayCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }

        OverlayCanvas.ReleaseMouseCapture();
        RenderEdits();
    }

    private void AddDragEdit(WpfPoint start, WpfPoint end)
    {
        if (_selectedTool is ScreenshotEditTool.Pixelate or ScreenshotEditTool.RedCircle)
        {
            var region = RedactionCoordinateMapper.ToImageRegion(
                start.X,
                start.Y,
                end.X,
                end.Y,
                OverlayCanvas.ActualWidth,
                OverlayCanvas.ActualHeight,
                _imageWidth,
                _imageHeight,
                _selectedTool == ScreenshotEditTool.RedCircle ? RedactionMode.RedCircle : RedactionMode.Pixelate);

            if (region is not null)
            {
                _redactions.Add(region);
                HasChanges = true;
            }

            return;
        }

        if (_selectedTool == ScreenshotEditTool.Arrow)
        {
            var imageStart = RedactionCoordinateMapper.DisplayToImagePoint(start.X, start.Y, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight, _imageWidth, _imageHeight);
            var imageEnd = RedactionCoordinateMapper.DisplayToImagePoint(end.X, end.Y, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight, _imageWidth, _imageHeight);
            if (imageStart is null || imageEnd is null || Distance(imageStart.Value, imageEnd.Value) < 5)
            {
                return;
            }

            _annotations.Add(new ScreenshotAnnotation
            {
                Type = ScreenshotAnnotationType.Arrow,
                X1 = imageStart.Value.X,
                Y1 = imageStart.Value.Y,
                X2 = imageEnd.Value.X,
                Y2 = imageEnd.Value.Y,
                Color = "#D92D20",
                StrokeThickness = 3
            });
            HasChanges = true;
            return;
        }

        var rectangle = RedactionCoordinateMapper.ToImageRegion(
            start.X,
            start.Y,
            end.X,
            end.Y,
            OverlayCanvas.ActualWidth,
            OverlayCanvas.ActualHeight,
            _imageWidth,
            _imageHeight,
            RedactionMode.Pixelate);

        if (rectangle is null)
        {
            return;
        }

        _annotations.Add(new ScreenshotAnnotation
        {
            Type = _selectedTool == ScreenshotEditTool.Highlight ? ScreenshotAnnotationType.Highlight : ScreenshotAnnotationType.Rectangle,
            X1 = rectangle.X,
            Y1 = rectangle.Y,
            X2 = rectangle.X + rectangle.Width,
            Y2 = rectangle.Y + rectangle.Height,
            Color = _selectedTool == ScreenshotEditTool.Highlight ? "#FFD84D" : "#D92D20",
            Opacity = _selectedTool == ScreenshotEditTool.Highlight ? 0.35 : 1,
            StrokeThickness = 3
        });
        HasChanges = true;
    }

    private void AddMarker(WpfPoint displayPoint)
    {
        var imagePoint = RedactionCoordinateMapper.DisplayToImagePoint(
            displayPoint.X,
            displayPoint.Y,
            OverlayCanvas.ActualWidth,
            OverlayCanvas.ActualHeight,
            _imageWidth,
            _imageHeight);
        if (imagePoint is null)
        {
            return;
        }

        var text = _selectedTool == ScreenshotEditTool.NumberMarker
            ? _nextMarkerNumber++.ToString()
            : (_nextMarkerLetter++).ToString();

        _annotations.Add(new ScreenshotAnnotation
        {
            Type = ScreenshotAnnotationType.Marker,
            X1 = imagePoint.Value.X,
            Y1 = imagePoint.Value.Y,
            X2 = 18,
            Y2 = 18,
            Text = text,
            Color = "#D92D20",
            StrokeThickness = 3
        });
        HasChanges = true;
        RenderEdits();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_annotations.Count > 0)
        {
            _annotations.RemoveAt(_annotations.Count - 1);
            HasChanges = true;
        }
        else if (_redactions.Count > 0)
        {
            _redactions.RemoveAt(_redactions.Count - 1);
            HasChanges = true;
        }

        RenderEdits();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _redactions.Clear();
        _annotations.Clear();
        ResetMarkerSequence();
        HasChanges = true;
        RenderEdits();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Crop_Click(object sender, RoutedEventArgs e)
    {
        CropRequested = true;
        DialogResult = true;
        Close();
    }

    private void RenderEdits()
    {
        OverlayCanvas.Children.Clear();
        foreach (var region in _redactions)
        {
            RenderRedaction(region);
        }

        foreach (var annotation in _annotations)
        {
            RenderAnnotation(annotation);
        }
    }

    private void RenderRedaction(RedactionRegion region)
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
        else if (region.Mode == RedactionMode.BlackBox)
        {
            var rectangle = CreateOverlayRectangle(WpfBrushes.Black, WpfBrushes.White, 1);
            Canvas.SetLeft(rectangle, display.X);
            Canvas.SetTop(rectangle, display.Y);
            rectangle.Width = display.Width;
            rectangle.Height = display.Height;
            OverlayCanvas.Children.Add(rectangle);
        }
        else if (region.Mode == RedactionMode.RedCircle)
        {
            OverlayCanvas.Children.Add(CreateRedCircle(display));
        }
    }

    private void RenderAnnotation(ScreenshotAnnotation annotation)
    {
        if (annotation.Type == ScreenshotAnnotationType.Rectangle)
        {
            var display = ToDisplayRectangle(annotation);
            var rectangle = CreateOverlayRectangle(WpfBrushes.Transparent, WpfBrushes.Red, annotation.StrokeThickness);
            Canvas.SetLeft(rectangle, display.X);
            Canvas.SetTop(rectangle, display.Y);
            rectangle.Width = display.Width;
            rectangle.Height = display.Height;
            OverlayCanvas.Children.Add(rectangle);
        }
        else if (annotation.Type == ScreenshotAnnotationType.Highlight)
        {
            var display = ToDisplayRectangle(annotation);
            var rectangle = CreateOverlayRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(95, 255, 216, 77)), new SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 154, 0)), 1);
            Canvas.SetLeft(rectangle, display.X);
            Canvas.SetTop(rectangle, display.Y);
            rectangle.Width = display.Width;
            rectangle.Height = display.Height;
            OverlayCanvas.Children.Add(rectangle);
        }
        else if (annotation.Type == ScreenshotAnnotationType.Arrow)
        {
            RenderArrow(annotation);
        }
        else if (annotation.Type == ScreenshotAnnotationType.Marker)
        {
            RenderMarker(annotation);
        }
    }

    private void RenderArrow(ScreenshotAnnotation annotation)
    {
        var start = ToDisplayPoint(annotation.X1, annotation.Y1);
        var end = ToDisplayPoint(annotation.X2, annotation.Y2);
        var line = new WpfLine
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = WpfBrushes.Red,
            StrokeThickness = annotation.StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        OverlayCanvas.Children.Add(line);

        var head = CreateArrowHead(start, end, annotation.StrokeThickness);
        OverlayCanvas.Children.Add(head);
    }

    private void RenderMarker(ScreenshotAnnotation annotation)
    {
        var center = ToDisplayPoint(annotation.X1, annotation.Y1);
        var scale = GetImageScale();
        var radius = Math.Max(10, annotation.X2 * scale);
        var marker = new Grid
        {
            Width = radius * 2,
            Height = radius * 2
        };
        marker.Children.Add(new WpfEllipse
        {
            Fill = WpfBrushes.Red,
            Stroke = WpfBrushes.White,
            StrokeThickness = 2
        });
        marker.Children.Add(new TextBlock
        {
            Text = annotation.Text,
            Foreground = WpfBrushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = Math.Max(10, radius),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        });
        Canvas.SetLeft(marker, center.X - radius);
        Canvas.SetTop(marker, center.Y - radius);
        OverlayCanvas.Children.Add(marker);
    }

    private void PixelateTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.Pixelate);

    private void RedCircleTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.RedCircle);

    private void RectangleTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.Rectangle);

    private void HighlightTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.Highlight);

    private void ArrowTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.Arrow);

    private void NumberTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.NumberMarker);

    private void LetterTool_Click(object sender, RoutedEventArgs e) => SelectTool(ScreenshotEditTool.LetterMarker);

    private void ResetSequence_Click(object sender, RoutedEventArgs e) => ResetMarkerSequence();

    private void SelectTool(ScreenshotEditTool tool)
    {
        _selectedTool = tool;
        PixelateToolButton.FontWeight = tool == ScreenshotEditTool.Pixelate ? FontWeights.SemiBold : FontWeights.Normal;
        RedCircleToolButton.FontWeight = tool == ScreenshotEditTool.RedCircle ? FontWeights.SemiBold : FontWeights.Normal;
        RectangleToolButton.FontWeight = tool == ScreenshotEditTool.Rectangle ? FontWeights.SemiBold : FontWeights.Normal;
        HighlightToolButton.FontWeight = tool == ScreenshotEditTool.Highlight ? FontWeights.SemiBold : FontWeights.Normal;
        ArrowToolButton.FontWeight = tool == ScreenshotEditTool.Arrow ? FontWeights.SemiBold : FontWeights.Normal;
        NumberToolButton.FontWeight = tool == ScreenshotEditTool.NumberMarker ? FontWeights.SemiBold : FontWeights.Normal;
        LetterToolButton.FontWeight = tool == ScreenshotEditTool.LetterMarker ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void ResetMarkerSequence()
    {
        _nextMarkerNumber = 1;
        _nextMarkerLetter = 'A';
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
        var topLeft = ToDisplayPoint(region.X, region.Y);
        var scale = GetImageScale();
        return new Rect(topLeft.X, topLeft.Y, region.Width * scale, region.Height * scale);
    }

    private Rect ToDisplayRectangle(ScreenshotAnnotation annotation)
    {
        var start = ToDisplayPoint(Math.Min(annotation.X1, annotation.X2), Math.Min(annotation.Y1, annotation.Y2));
        var end = ToDisplayPoint(Math.Max(annotation.X1, annotation.X2), Math.Max(annotation.Y1, annotation.Y2));
        return new Rect(start.X, start.Y, Math.Max(0, end.X - start.X), Math.Max(0, end.Y - start.Y));
    }

    private WpfPoint ToDisplayPoint(double imageX, double imageY)
    {
        var scale = GetImageScale();
        var displayedWidth = _imageWidth * scale;
        var displayedHeight = _imageHeight * scale;
        var offsetX = (OverlayCanvas.ActualWidth - displayedWidth) / 2;
        var offsetY = (OverlayCanvas.ActualHeight - displayedHeight) / 2;
        return new WpfPoint(offsetX + imageX * scale, offsetY + imageY * scale);
    }

    private double GetImageScale()
    {
        if (OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0 || _imageWidth <= 0 || _imageHeight <= 0)
        {
            return 1;
        }

        return Math.Min(OverlayCanvas.ActualWidth / _imageWidth, OverlayCanvas.ActualHeight / _imageHeight);
    }

    private Shape CreatePreviewShape()
    {
        return _selectedTool switch
        {
            ScreenshotEditTool.RedCircle => new WpfEllipse
            {
                Fill = WpfBrushes.Transparent,
                Stroke = WpfBrushes.Red,
                StrokeThickness = 4,
                Opacity = 0.9
            },
            ScreenshotEditTool.Arrow => new WpfLine
            {
                Stroke = WpfBrushes.Red,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            },
            ScreenshotEditTool.Highlight => CreateOverlayRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(95, 255, 216, 77)), new SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 154, 0)), 1),
            ScreenshotEditTool.Rectangle => CreateOverlayRectangle(WpfBrushes.Transparent, WpfBrushes.Red, 3),
            _ => CreateOverlayRectangle(WpfBrushes.Transparent, WpfBrushes.White, 2)
        };
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

    private static WpfEllipse CreateRedCircle(Rect display)
    {
        var ellipse = new WpfEllipse
        {
            Fill = WpfBrushes.Transparent,
            Stroke = WpfBrushes.Red,
            StrokeThickness = Math.Max(3, Math.Min(display.Width, display.Height) / 18),
            Width = display.Width,
            Height = display.Height,
            Opacity = 0.95
        };
        Canvas.SetLeft(ellipse, display.X);
        Canvas.SetTop(ellipse, display.Y);
        return ellipse;
    }

    private static Polygon CreateArrowHead(WpfPoint start, WpfPoint end, double strokeThickness)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var length = Math.Max(12, strokeThickness * 5);
        var spread = Math.PI / 7;
        var point1 = new WpfPoint(end.X - length * Math.Cos(angle - spread), end.Y - length * Math.Sin(angle - spread));
        var point2 = new WpfPoint(end.X - length * Math.Cos(angle + spread), end.Y - length * Math.Sin(angle + spread));
        return new Polygon
        {
            Fill = WpfBrushes.Red,
            Points = new PointCollection(new[] { end, point1, point2 })
        };
    }

    private static void PositionPreviewShape(Shape shape, WpfPoint start, WpfPoint end)
    {
        if (shape is WpfLine line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
            return;
        }

        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        shape.Width = Math.Abs(end.X - start.X);
        shape.Height = Math.Abs(end.Y - start.Y);
    }

    private static double Distance((double X, double Y) start, (double X, double Y) end)
    {
        var dx = start.X - end.X;
        var dy = start.Y - end.Y;
        return Math.Sqrt(dx * dx + dy * dy);
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

    private enum ScreenshotEditTool
    {
        Pixelate,
        RedCircle,
        Rectangle,
        Highlight,
        Arrow,
        NumberMarker,
        LetterMarker
    }
}
