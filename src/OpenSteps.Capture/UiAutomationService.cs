using System.Text;
using System.Windows.Automation;
using OpenSteps.Core.Models;

namespace OpenSteps.Capture;

public sealed class UiAutomationService
{
    private static readonly string[] UsefulControlTypes =
    [
        "Button",
        "MenuItem",
        "TabItem",
        "Edit",
        "Hyperlink",
        "ListItem",
        "ComboBox",
        "CheckBox",
        "RadioButton"
    ];

    public UiElementInfo GetElementAt(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (element is null)
            {
                return Failed("AutomationElement.FromPoint returned no element.");
            }

            var rawElement = Snapshot(element);
            var parentChain = BuildParentChain(element);
            var candidates = CollectCandidates(element, x, y);
            var chosen = ChooseElement(rawElement, candidates, x, y);
            var quality = IsUseful(chosen)
                ? UiAutomationQuality.UsefulElementFound
                : UiAutomationQuality.GenericContainerOnly;

            return new UiElementInfo(
                chosen.Name,
                chosen.AutomationId,
                chosen.ControlType,
                chosen.ClassName,
                chosen.Bounds,
                parentChain.FirstOrDefault()?.Name,
                quality,
                quality == UiAutomationQuality.UsefulElementFound,
                FormatElement(rawElement),
                FormatParentChain(parentChain),
                FormatCandidates(candidates, x, y));
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    private static ElementSnapshot ChooseElement(ElementSnapshot rawElement, IReadOnlyList<ElementSnapshot> candidates, int x, int y)
    {
        if (IsUseful(rawElement) && Contains(rawElement.Bounds, x, y))
        {
            return rawElement;
        }

        var best = candidates
            .Where(candidate => candidate.ContainsClick && IsUseful(candidate))
            .OrderBy(candidate => Area(candidate.Bounds))
            .ThenBy(candidate => DistanceFromCenter(candidate.Bounds, x, y))
            .FirstOrDefault();

        return best ?? rawElement;
    }

    private static IReadOnlyList<ElementSnapshot> CollectCandidates(AutomationElement root, int x, int y)
    {
        var candidates = new List<ElementSnapshot>();
        var walker = TreeWalker.RawViewWalker;
        const int maxDepth = 4;
        const int maxElements = 80;

        void Visit(AutomationElement element, int depth)
        {
            if (depth > maxDepth || candidates.Count >= maxElements)
            {
                return;
            }

            AutomationElement? child = null;
            try
            {
                child = walker.GetFirstChild(element);
            }
            catch
            {
                return;
            }

            while (child is not null && candidates.Count < maxElements)
            {
                var snapshot = Snapshot(child, x, y, depth);
                if (snapshot.ContainsClick || IsUseful(snapshot))
                {
                    candidates.Add(snapshot);
                }

                Visit(child, depth + 1);

                try
                {
                    child = walker.GetNextSibling(child);
                }
                catch
                {
                    break;
                }
            }
        }

        Visit(root, 1);
        return candidates;
    }

    private static IReadOnlyList<ElementSnapshot> BuildParentChain(AutomationElement element)
    {
        var parents = new List<ElementSnapshot>();
        var walker = TreeWalker.RawViewWalker;
        var current = element;

        for (var i = 0; i < 5; i++)
        {
            try
            {
                current = walker.GetParent(current);
            }
            catch
            {
                break;
            }

            if (current is null)
            {
                break;
            }

            parents.Add(Snapshot(current));
        }

        return parents;
    }

    private static ElementSnapshot Snapshot(AutomationElement element, int? clickX = null, int? clickY = null, int depth = 0)
    {
        try
        {
            var current = element.Current;
            var bounds = ToBounds(current.BoundingRectangle);
            return new ElementSnapshot(
                EmptyToNull(current.Name),
                EmptyToNull(current.AutomationId),
                EmptyToNull(current.ControlType?.ProgrammaticName?.Replace("ControlType.", "", StringComparison.Ordinal)),
                EmptyToNull(current.ClassName),
                bounds,
                clickX.HasValue && clickY.HasValue && Contains(bounds, clickX.Value, clickY.Value),
                depth);
        }
        catch (Exception ex)
        {
            return new ElementSnapshot(null, null, null, null, null, false, depth, ex.Message);
        }
    }

    private static UiElementInfo Failed(string message)
    {
        return new UiElementInfo(
            null,
            null,
            null,
            null,
            null,
            null,
            UiAutomationQuality.UiAutomationFailed,
            false,
            $"UI Automation failed: {message}",
            string.Empty,
            string.Empty);
    }

    private static bool IsUseful(ElementSnapshot element)
    {
        var hasName = !string.IsNullOrWhiteSpace(element.Name);
        var hasAutomationId = !string.IsNullOrWhiteSpace(element.AutomationId);
        var usefulControl = UsefulControlTypes.Any(type => IsControlType(element.ControlType, type));

        if (IsGenericContainer(element))
        {
            return false;
        }

        return usefulControl && (hasName || hasAutomationId);
    }

    private static bool IsGenericContainer(ElementSnapshot element)
    {
        var genericClass = string.Equals(element.ClassName, "Microsoft.UI.Content.DesktopChildSiteBridge", StringComparison.OrdinalIgnoreCase);
        var genericPane = IsControlType(element.ControlType, "Pane")
            && string.IsNullOrWhiteSpace(element.Name)
            && string.IsNullOrWhiteSpace(element.AutomationId);

        return genericClass || genericPane;
    }

    private static bool IsControlType(string? actual, string expected)
    {
        return actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ScreenBounds? ToBounds(System.Windows.Rect rect)
    {
        return rect.IsEmpty
            ? null
            : new ScreenBounds((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    }

    private static bool Contains(ScreenBounds? bounds, int x, int y)
    {
        return bounds is { } value
            && x >= value.X
            && y >= value.Y
            && x <= value.X + value.Width
            && y <= value.Y + value.Height;
    }

    private static long Area(ScreenBounds? bounds)
    {
        return bounds is { } value ? Math.Max(1L, (long)value.Width * value.Height) : long.MaxValue;
    }

    private static double DistanceFromCenter(ScreenBounds? bounds, int x, int y)
    {
        if (bounds is not { } value)
        {
            return double.MaxValue;
        }

        var centerX = value.X + value.Width / 2.0;
        var centerY = value.Y + value.Height / 2.0;
        return Math.Sqrt(Math.Pow(centerX - x, 2) + Math.Pow(centerY - y, 2));
    }

    private static string FormatElement(ElementSnapshot element)
    {
        return FormatElement(element, null);
    }

    private static string FormatElement(ElementSnapshot element, int? clickX)
    {
        var suffix = clickX.HasValue ? $", contains click: {(element.ContainsClick ? "yes" : "no")}" : string.Empty;
        var error = string.IsNullOrWhiteSpace(element.Error) ? string.Empty : $", error: {element.Error}";
        return $"depth {element.Depth}: name={element.Name ?? "(empty)"}, control={element.ControlType ?? "(unknown)"}, class={element.ClassName ?? "(none)"}, automationId={element.AutomationId ?? "(none)"}, bounds={FormatBounds(element.Bounds)}{suffix}{error}";
    }

    private static string FormatParentChain(IReadOnlyList<ElementSnapshot> parents)
    {
        if (parents.Count == 0)
        {
            return "(none)";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < parents.Count; i++)
        {
            builder.AppendLine($"parent {i + 1}: {FormatElement(parents[i])}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatCandidates(IReadOnlyList<ElementSnapshot> candidates, int x, int y)
    {
        if (candidates.Count == 0)
        {
            return "(none found within RawView search limits)";
        }

        var builder = new StringBuilder();
        foreach (var candidate in candidates.Take(20))
        {
            builder.AppendLine(FormatElement(candidate, x));
        }

        if (candidates.Count > 20)
        {
            builder.AppendLine($"... {candidates.Count - 20} more candidates omitted");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBounds(ScreenBounds? bounds)
    {
        return bounds is { } value
            ? $"({value.X}, {value.Y}) {value.Width}x{value.Height}"
            : "(unknown)";
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ElementSnapshot(
        string? Name,
        string? AutomationId,
        string? ControlType,
        string? ClassName,
        ScreenBounds? Bounds,
        bool ContainsClick = false,
        int Depth = 0,
        string? Error = null);
}
