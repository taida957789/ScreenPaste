using ScreenPaste.Input;
using ScreenPaste.Native;

namespace ScreenPaste.Settings;

/// <summary>How the settings windows name an in-app action and spell a key.</summary>
internal static class ShortcutText
{
    /// <summary>"Copy", "Move the area or annotation ↑".</summary>
    public static string Name(ShortcutAction action) =>
        action.LabelArg is { } arg ? Loc.T(action.LabelKey, arg) : Loc.T(action.LabelKey);

    /// <summary>"Ctrl+T", "[", "Shift+1" for the "!" Shift+1 types on the current layout,
    /// "Shift+[" for Shift+OemOpenBrackets (<see cref="ShortcutKey.ToDisplayString"/>), or
    /// "Shift (tap)" for a modifier tapped on its own. Tooltips and menus keep the stored text.</summary>
    public static string Key(ShortcutKey key) =>
        key.Kind == ShortcutKeyKind.Tap
            ? Loc.T("keys.tap", key.ToString())
            : key.ToDisplayString(KeyboardLayout.ShiftedKeyOf, KeyboardLayout.UnshiftedText);

    /// <summary>A global hotkey as its field shows it ("Shift+[" for "Shift+OemOpenBrackets");
    /// text that names no key as it is.</summary>
    public static string Gesture(string gesture) =>
        KeyChord.Parse(gesture) is { } chord ? chord.ToDisplayString(KeyboardLayout.UnshiftedText) : gesture;
}
