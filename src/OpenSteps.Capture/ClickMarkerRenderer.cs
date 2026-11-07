using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed record ClickMarkerAppearance(ClickMarkerStyle Style, Color Color, ClickMarkerSize Size)
{
    public static ClickMarkerAppearance Default { get; } = new(ClickMarkerStyle.Ring, Color.FromArgb(220, 30, 30), ClickMarkerSize.Medium);
}

public static class ClickMarkerRenderer
{
    /// <summary>Appearance used for every marker drawn from now on; set from the user's settings.</summary>
    public static ClickMarkerAppearance Appearance { get; set; } = ClickMarkerAppearance.Default;

    public static void Draw(Graphics graphics, int x, int y)
    {
        var appearance = Appearance;
        var radius = appearance.Size switch
        {
            ClickMarkerSize.Small => 16,
            ClickMarkerSize.Large => 30,
            _ => 22
        };

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(x - radius, y - radius, radius * 2, radius * 2);
        if (appearance.Style == ClickMarkerStyle.Filled)
        {
            using var fill = new SolidBrush(Color.FromArgb(90, appearance.Color));
            using var outline = new Pen(Color.FromArgb(230, appearance.Color), 2);
            graphics.FillEllipse(fill, bounds);
            graphics.DrawEllipse(outline, bounds);
            return;
        }

        using var pen = new Pen(Color.FromArgb(230, appearance.Color), Math.Max(3, radius * 5 / 22));
        graphics.DrawEllipse(pen, bounds);
    }

    /// <summary>Writes a copy of <paramref name="sourcePath"/> with the click circle drawn at (x, y).</summary>
    public static void Render(string sourcePath, string outputPath, int x, int y)
    {
        using var source = new Bitmap(sourcePath);
        using var marked = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(marked))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            Draw(graphics, x, y);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        marked.Save(outputPath, ImageFormat.Png);
    }
}
