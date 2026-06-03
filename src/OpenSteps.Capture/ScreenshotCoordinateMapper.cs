namespace OpenSteps.Capture;

public static class ScreenshotCoordinateMapper
{
    public static (int X, int Y) ToCapturedPoint(int globalX, int globalY, int captureLeft, int captureTop)
    {
        return (globalX - captureLeft, globalY - captureTop);
    }

    public static bool IsInsideCapturedBounds(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }
}
