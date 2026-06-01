using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public static class RedactionCoordinateMapper
{
    public static (double X, double Y)? DisplayToImagePoint(
        double mouseX,
        double mouseY,
        double containerWidth,
        double containerHeight,
        int imageWidth,
        int imageHeight)
    {
        var point = ConvertDisplayToImagePoint(mouseX, mouseY, containerWidth, containerHeight, imageWidth, imageHeight, allowOutside: false);
        return point;
    }

    public static RedactionRegion? ToImageRegion(
        double startX,
        double startY,
        double endX,
        double endY,
        double containerWidth,
        double containerHeight,
        int imageWidth,
        int imageHeight,
        RedactionMode mode,
        int minimumSize = 5)
    {
        var start = ConvertDisplayToImagePoint(startX, startY, containerWidth, containerHeight, imageWidth, imageHeight, allowOutside: true);
        var end = ConvertDisplayToImagePoint(endX, endY, containerWidth, containerHeight, imageWidth, imageHeight, allowOutside: true);
        if (start is null || end is null)
        {
            return null;
        }

        var left = Math.Clamp(Math.Min(start.Value.X, end.Value.X), 0, imageWidth);
        var top = Math.Clamp(Math.Min(start.Value.Y, end.Value.Y), 0, imageHeight);
        var right = Math.Clamp(Math.Max(start.Value.X, end.Value.X), 0, imageWidth);
        var bottom = Math.Clamp(Math.Max(start.Value.Y, end.Value.Y), 0, imageHeight);
        var width = (int)Math.Round(right - left);
        var height = (int)Math.Round(bottom - top);
        if (width < minimumSize || height < minimumSize)
        {
            return null;
        }

        return new RedactionRegion((int)Math.Round(left), (int)Math.Round(top), width, height, mode);
    }

    private static (double X, double Y)? ConvertDisplayToImagePoint(
        double mouseX,
        double mouseY,
        double containerWidth,
        double containerHeight,
        int imageWidth,
        int imageHeight,
        bool allowOutside)
    {
        if (containerWidth <= 0 || containerHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        var scale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);
        var displayedWidth = imageWidth * scale;
        var displayedHeight = imageHeight * scale;
        var offsetX = (containerWidth - displayedWidth) / 2;
        var offsetY = (containerHeight - displayedHeight) / 2;

        if (!allowOutside && (mouseX < offsetX || mouseY < offsetY || mouseX > offsetX + displayedWidth || mouseY > offsetY + displayedHeight))
        {
            return null;
        }

        return ((mouseX - offsetX) / scale, (mouseY - offsetY) / scale);
    }
}
