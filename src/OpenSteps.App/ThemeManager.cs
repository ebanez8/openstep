using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using OpenSteps.Core.Models;

namespace OpenSteps.App;

/// <summary>
/// Swaps the colour palette (Themes/Light.xaml or Themes/Dark.xaml) and accent brushes at runtime.
/// All XAML references palette brushes with DynamicResource, so open windows update in place.
/// </summary>
internal static class ThemeManager
{
    private const int DwmUseImmersiveDarkMode = 20;

    private static readonly string[] AccentKeys =
    [
        "AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "TextInverseBrush", "FocusBorderBrush"
    ];

    private static AppSettings? _settings;
    private static bool _initialized;

    public static bool IsDark { get; private set; }

    public static void Apply(AppSettings settings)
    {
        _settings = settings;
        Initialize();

        var resources = System.Windows.Application.Current.Resources;
        IsDark = settings.Theme == AppTheme.Dark
            || (settings.Theme == AppTheme.System && !IsSystemLightTheme());
        resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(IsDark ? "Dark" : "Light")}.xaml")
        };

        foreach (var key in AccentKeys)
        {
            resources.Remove(key);
        }

        var accent = settings.AccentColorMode switch
        {
            AccentColorMode.System => ReadSystemAccent(IsDark),
            AccentColorMode.Custom => TryParseColor(settings.CustomAccentColor),
            _ => null
        };

        if (accent is { } color)
        {
            // Above ~0.22 relative luminance, dark text has better contrast than white.
            var isLightAccent = Luminance(color) > 0.22;
            resources["AccentBrush"] = Frozen(color);
            resources["AccentHoverBrush"] = Frozen(Mix(color, isLightAccent ? Colors.Black : Colors.White, 0.12));
            resources["AccentPressedBrush"] = Frozen(Mix(color, Colors.Black, 0.18));
            resources["TextInverseBrush"] = Frozen(isLightAccent ? System.Windows.Media.Color.FromRgb(0x0E, 0x16, 0x26) : Colors.White);
            resources["FocusBorderBrush"] = Frozen(color);
        }

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }
    }

    public static System.Windows.Media.Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !System.Text.RegularExpressions.Regex.IsMatch(hex.Trim(), "^#?[0-9A-Fa-f]{6}$"))
        {
            return null;
        }

        var value = Convert.ToInt32(hex.Trim().TrimStart('#'), 16);
        return System.Windows.Media.Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => ApplyTitleBar((Window)sender)));
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        System.Windows.Application.Current.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_settings is not { } settings
            || (settings.Theme != AppTheme.System && settings.AccentColorMode != AccentColorMode.System)
            || e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Apply(settings));
    }

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    private static bool IsSystemLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    /// <summary>
    /// Windows stores accent shades as Light3, Light2, Light1, Base, Dark1, Dark2, Dark3. Like Windows 11
    /// controls, use Dark1 on light backgrounds and Light2 on dark backgrounds.
    /// </summary>
    private static System.Windows.Media.Color ReadSystemAccent(bool dark)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
        if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
        {
            var offset = (dark ? 1 : 4) * 4;
            return System.Windows.Media.Color.FromRgb(palette[offset], palette[offset + 1], palette[offset + 2]);
        }

        var glass = SystemParameters.WindowGlassColor;
        return System.Windows.Media.Color.FromRgb(glass.R, glass.G, glass.B);
    }

    private static double Luminance(System.Windows.Media.Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static System.Windows.Media.Color Mix(System.Windows.Media.Color from, System.Windows.Media.Color to, double amount)
    {
        byte Lerp(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return System.Windows.Media.Color.FromRgb(Lerp(from.R, to.R), Lerp(from.G, to.G), Lerp(from.B, to.B));
    }

    private static SolidColorBrush Frozen(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
