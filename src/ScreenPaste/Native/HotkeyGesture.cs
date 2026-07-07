using System.Windows.Input;
using static ScreenPaste.Native.NativeMethods;

namespace ScreenPaste.Native;

/// <summary>
/// Parses human-friendly hotkey strings ("F1", "Ctrl+Shift+A", "Ctrl+Z") into WPF
/// <see cref="KeyGesture"/>s, and converts the capture hotkey into Win32 RegisterHotKey args.
/// </summary>
public static class HotkeyGesture
{
    private static readonly KeyGestureConverter Converter = new();

    /// <summary>Parse a gesture string; returns null if invalid.</summary>
    public static KeyGesture? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return Converter.ConvertFromString(text) as KeyGesture; }
        catch { return null; }
    }

    /// <summary>Convert a gesture string to (Win32 modifier flags, virtual-key) for RegisterHotKey.</summary>
    public static (uint modifiers, uint virtualKey)? ToWin32(string? text)
    {
        var g = Parse(text);
        if (g == null) return null;

        uint mods = 0;
        if (g.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= MOD_ALT;
        if (g.Modifiers.HasFlag(ModifierKeys.Control)) mods |= MOD_CONTROL;
        if (g.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= MOD_SHIFT;
        if (g.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(g.Key);
        if (vk == 0) return null;
        return (mods, vk);
    }
}
