using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenSteps.Core.Models;

namespace OpenSteps.App;

public partial class SettingsWindow : Window
{
    private const string HotkeyHint = "Click the button, then press a shortcut with Ctrl or Alt.";

    private static readonly (string Name, string Hex)[] MarkerColors =
    [
        ("Red", "#DC1E1E"), ("Orange", "#F97316"), ("Yellow", "#FACC15"),
        ("Green", "#16A34A"), ("Blue", "#2563EB"), ("Pink", "#DB2777")
    ];

    private static readonly (string Name, string Hex)[] AccentColors =
    [
        ("Blue", "#0078D4"), ("Indigo", "#4F46E5"), ("Purple", "#8764B8"), ("Pink", "#E3008C"),
        ("Red", "#D13438"), ("Orange", "#CA5010"), ("Green", "#107C10"), ("Teal", "#038387")
    ];

    private readonly MainWindow _owner;
    private readonly AppSettings _settings;
    private bool _loading = true;

    internal SettingsWindow(MainWindow owner, AppSettings settings, string? activeHotkey)
    {
        InitializeComponent();
        _owner = owner;
        _settings = settings;

        CaptureModeCombo.SelectedIndex = settings.ScreenshotMode == ScreenshotMode.ActiveWindow ? 1 : 0;
        HotkeyEnabledCheckBox.IsChecked = settings.RecordingHotkeyEnabled;
        HotkeyBox.Content = FormatHotkey(settings.RecordingHotkey);
        KeyboardCheckBox.IsChecked = settings.RecordKeyboardInput;
        MarkerCheckBox.IsChecked = settings.ShowClickMarker;
        MarkerStyleCombo.SelectedIndex = (int)settings.ClickMarkerStyle;
        MarkerSizeCombo.SelectedIndex = (int)settings.ClickMarkerSize;
        ThemeCombo.SelectedIndex = (int)settings.Theme;
        AccentCombo.SelectedIndex = (int)settings.AccentColorMode;
        AccentHexBox.Text = settings.CustomAccentColor;
        StartupCheckBox.IsChecked = StartupRegistration.IsEnabled();

        BuildSwatches(MarkerSwatches, "marker", MarkerColors, settings.ClickMarkerColor, hex =>
        {
            _settings.ClickMarkerColor = hex;
            ApplyMarker();
        });
        BuildSwatches(AccentSwatches, "accent", AccentColors, settings.CustomAccentColor, hex =>
        {
            AccentHexBox.Text = hex;
        });

        if (settings.RecordingHotkeyEnabled && activeHotkey is null)
        {
            ShowHotkeyStatus($"{FormatHotkey(settings.RecordingHotkey)} isn't active. Another app may already use it.", isError: true);
        }

        UpdateVisibility();
        UpdateMarkerPreview();
        _loading = false;
    }

    private void CaptureModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.ScreenshotMode = CaptureModeCombo.SelectedIndex == 1 ? ScreenshotMode.ActiveWindow : ScreenshotMode.FullDesktop;
    }

    private void HotkeyEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.RecordingHotkeyEnabled = HotkeyEnabledCheckBox.IsChecked == true;
        var error = _owner.ApplySettings();
        ShowHotkeyStatus(error ?? HotkeyHint, error is not null);
        UpdateVisibility();
    }

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyBox.Content = "Press a shortcut…";
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyBox.Content = FormatHotkey(_settings.RecordingHotkey);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var (wpfModifier, modifier) in new[]
        {
            (ModifierKeys.Control, HotkeyModifiers.Ctrl),
            (ModifierKeys.Alt, HotkeyModifiers.Alt),
            (ModifierKeys.Shift, HotkeyModifiers.Shift),
            (ModifierKeys.Windows, HotkeyModifiers.Win)
        })
        {
            if (Keyboard.Modifiers.HasFlag(wpfModifier))
            {
                modifiers |= modifier;
            }
        }

        // Let Tab and Escape (without modifiers) move focus away as usual.
        if (modifiers == HotkeyModifiers.None && key is Key.Tab or Key.Escape)
        {
            if (key == Key.Escape)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;
        var gesture = new HotkeyGesture(modifiers, key.ToString());
        var problem = ValidateHotkey(gesture);
        if (problem is not null)
        {
            ShowHotkeyStatus(problem, isError: true);
            return;
        }

        var previous = _settings.RecordingHotkey;
        _settings.RecordingHotkey = gesture.ToString();
        var error = _owner.ApplySettings();
        if (error is not null)
        {
            _settings.RecordingHotkey = previous;
            _owner.ApplySettings();
            ShowHotkeyStatus($"{gesture} is already used by another app. Try a different shortcut.", isError: true);
            return;
        }

        HotkeyBox.Content = gesture.ToString();
        ShowHotkeyStatus($"Saved. Press {gesture} anywhere to start or stop recording.", isError: false);
        Keyboard.ClearFocus();
    }

    private void KeyboardCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.RecordKeyboardInput = KeyboardCheckBox.IsChecked == true;
    }

    private void MarkerCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.ShowClickMarker = MarkerCheckBox.IsChecked == true;
        UpdateMarkerPreview();
    }

    private void MarkerAppearance_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.ClickMarkerStyle = (ClickMarkerStyle)Math.Max(0, MarkerStyleCombo.SelectedIndex);
        _settings.ClickMarkerSize = (ClickMarkerSize)Math.Max(0, MarkerSizeCombo.SelectedIndex);
        ApplyMarker();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Theme = (AppTheme)Math.Max(0, ThemeCombo.SelectedIndex);
        _owner.ApplySettings();
    }

    private void AccentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.AccentColorMode = (AccentColorMode)Math.Max(0, AccentCombo.SelectedIndex);
        UpdateVisibility();
        _owner.ApplySettings();
    }

    private void AccentHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || ThemeManager.TryParseColor(AccentHexBox.Text) is not { } color)
        {
            return;
        }

        _settings.CustomAccentColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        SelectSwatch(AccentSwatches, _settings.CustomAccentColor);
        _owner.ApplySettings();
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            StartupRegistration.SetEnabled(StartupCheckBox.IsChecked == true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            StartupHint.Text = $"Couldn't change the Windows startup list: {ex.Message}";
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyMarker()
    {
        _owner.ApplySettings();
        UpdateMarkerPreview();
    }

    private void UpdateVisibility()
    {
        HotkeyPanel.Visibility = _settings.RecordingHotkeyEnabled ? Visibility.Visible : Visibility.Collapsed;
        CustomAccentPanel.Visibility = _settings.AccentColorMode == AccentColorMode.Custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMarkerPreview()
    {
        var color = ThemeManager.TryParseColor(_settings.ClickMarkerColor) ?? Colors.Red;
        var radius = _settings.ClickMarkerSize switch
        {
            ClickMarkerSize.Small => 16,
            ClickMarkerSize.Large => 30,
            _ => 22
        };

        MarkerPreview.Width = MarkerPreview.Height = radius * 2;
        MarkerPreview.Opacity = _settings.ShowClickMarker ? 1 : 0.3;
        if (_settings.ClickMarkerStyle == ClickMarkerStyle.Filled)
        {
            MarkerPreview.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, color.R, color.G, color.B));
            MarkerPreview.StrokeThickness = 2;
        }
        else
        {
            MarkerPreview.Fill = null;
            MarkerPreview.StrokeThickness = Math.Max(3, radius * 5 / 22);
        }

        MarkerPreview.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, color.R, color.G, color.B));
    }

    private void ShowHotkeyStatus(string text, bool isError)
    {
        HotkeyStatusText.Text = text;
        HotkeyStatusText.SetResourceReference(TextBlock.ForegroundProperty, isError ? "DangerBrush" : "TextSecondaryBrush");
    }

    private static string? ValidateHotkey(HotkeyGesture gesture)
    {
        if (!gesture.Modifiers.HasFlag(HotkeyModifiers.Ctrl) && !gesture.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            return "Include Ctrl or Alt so the shortcut doesn't get in the way of normal typing.";
        }

        // Other keys (Enter, arrows, Ctrl+C…) would themselves be recorded as guide steps.
        var key = gesture.Key;
        var isLetter = key.Length == 1 && char.IsAsciiLetterUpper(key[0]);
        var isDigit = key.Length == 2 && key[0] == 'D' && char.IsAsciiDigit(key[1]);
        var isFunctionKey = key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out var number) && number is >= 1 and <= 24;
        if (!isLetter && !isDigit && !isFunctionKey)
        {
            return "Use a letter, number or F-key together with Ctrl or Alt.";
        }

        if (isLetter && gesture.Modifiers.HasFlag(HotkeyModifiers.Ctrl) && key is "A" or "C" or "S" or "V" or "Z")
        {
            return $"Ctrl+{key} is recorded as a guide step. Pick a different letter.";
        }

        return null;
    }

    private static string FormatHotkey(string text)
    {
        return HotkeyGesture.TryParse(text, out var gesture) ? gesture.ToString() : text;
    }

    private static void BuildSwatches(WrapPanel panel, string group, (string Name, string Hex)[] colors, string selectedHex, Action<string> selected)
    {
        foreach (var (name, hex) in colors)
        {
            var swatch = new System.Windows.Controls.RadioButton
            {
                Style = (Style)panel.FindResource("SwatchRadio"),
                GroupName = group,
                Background = new SolidColorBrush(ThemeManager.TryParseColor(hex)!.Value),
                ToolTip = name,
                Tag = hex,
                IsChecked = string.Equals(hex, selectedHex, StringComparison.OrdinalIgnoreCase)
            };
            swatch.Checked += (_, _) => selected(hex);
            panel.Children.Add(swatch);
        }
    }

    private static void SelectSwatch(WrapPanel panel, string hex)
    {
        foreach (var swatch in panel.Children.OfType<System.Windows.Controls.RadioButton>())
        {
            var matches = string.Equals((string)swatch.Tag, hex, StringComparison.OrdinalIgnoreCase);
            if (swatch.IsChecked != matches)
            {
                swatch.IsChecked = matches;
            }
        }
    }
}
