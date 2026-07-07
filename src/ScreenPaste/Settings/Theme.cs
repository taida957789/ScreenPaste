using Microsoft.Win32;
using System.Windows.Media;

namespace ScreenPaste.Settings;

public enum ThemeMode { System, Light, Dark }

/// <summary>
/// Central light/dark palette. <see cref="Apply"/> resolves the effective mode
/// (following the OS when set to System), after which the color accessors reflect it.
/// UI that is rebuilt per use (the toolbar, dialogs) simply reads these each time.
/// </summary>
public static class Theme
{
    public static bool IsDark { get; private set; } = true;

    public static void Apply(string? mode) => Apply(Parse(mode));

    public static void Apply(ThemeMode mode) =>
        IsDark = mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => SystemUsesDark(),
        };

    public static ThemeMode Parse(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    private static bool SystemUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            // AppsUseLightTheme: 0 = dark, 1 = light
            if (key?.GetValue("AppsUseLightTheme") is int v) return v == 0;
        }
        catch { /* ignore */ }
        return true; // default dark
    }

    // ---- WPF colors (toolbar / dialogs) ----
    public static Color PanelBg => IsDark ? C(0xF2, 0x22, 0x22, 0x22) : C(0xF7, 0xFA, 0xFA, 0xFA);
    public static Color WindowBg => IsDark ? C(0xFF, 0x2A, 0x2A, 0x2A) : C(0xFF, 0xFA, 0xFA, 0xFA);
    public static Color Foreground => IsDark ? Colors.White : C(0xFF, 0x20, 0x20, 0x20);
    public static Color ButtonBg => IsDark ? C(0x22, 0xFF, 0xFF, 0xFF) : C(0x14, 0x00, 0x00, 0x00);
    public static Color ButtonBorder => IsDark ? C(0x33, 0xFF, 0xFF, 0xFF) : C(0x33, 0x00, 0x00, 0x00);
    public static Color Separator => IsDark ? C(0x44, 0xFF, 0xFF, 0xFF) : C(0x33, 0x00, 0x00, 0x00);
    public static Color Accent => C(0xFF, 0x3D, 0xA9, 0xFC);
    public static Color ActiveBg => IsDark ? C(0x88, 0x3D, 0xA9, 0xFC) : C(0x66, 0x3D, 0xA9, 0xFC);

    // Input controls (text boxes, combos) in dialogs.
    public static Color ControlBg => IsDark ? C(0xFF, 0x3A, 0x3A, 0x3A) : C(0xFF, 0xFF, 0xFF, 0xFF);
    public static Color ControlBorder => IsDark ? C(0xFF, 0x5A, 0x5A, 0x5A) : C(0xFF, 0xAB, 0xAD, 0xB3);

    public static SolidColorBrush PanelBrush => new(PanelBg);
    public static SolidColorBrush WindowBrush => new(WindowBg);
    public static SolidColorBrush ForegroundBrush => new(Foreground);
    public static SolidColorBrush ButtonBgBrush => new(ButtonBg);
    public static SolidColorBrush ButtonBorderBrush => new(ButtonBorder);
    public static SolidColorBrush SeparatorBrush => new(Separator);
    public static SolidColorBrush ActiveBrush => new(ActiveBg);
    public static SolidColorBrush ControlBgBrush => new(ControlBg);
    public static SolidColorBrush ControlBorderBrush => new(ControlBorder);

    private static Color C(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}
