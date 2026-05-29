using System.Windows;
using System.Windows.Automation;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class UiAutomationService
{
    public UiElementInfo? GetElementAt(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (element is null)
            {
                return null;
            }

            var bounds = element.Current.BoundingRectangle;
            ScreenBounds? screenBounds = bounds.IsEmpty
                ? null
                : new ScreenBounds((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);

            string? parentName = null;
            try
            {
                parentName = TreeWalker.ControlViewWalker.GetParent(element)?.Current.Name;
            }
            catch
            {
                parentName = null;
            }

            return new UiElementInfo(
                EmptyToNull(element.Current.Name),
                EmptyToNull(element.Current.AutomationId),
                EmptyToNull(element.Current.ControlType?.ProgrammaticName?.Replace("ControlType.", "", StringComparison.Ordinal)),
                EmptyToNull(element.Current.ClassName),
                screenBounds,
                EmptyToNull(parentName));
        }
        catch
        {
            return null;
        }
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
