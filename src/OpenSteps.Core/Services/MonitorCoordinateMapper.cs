namespace OpenSteps.Core.Services;

public static class MonitorCoordinateMapper
{
    public static (int X, int Y) ToLocalPoint(int clickX, int clickY, int boundsLeft, int boundsTop)
    {
        return (clickX - boundsLeft, clickY - boundsTop);
    }
}
