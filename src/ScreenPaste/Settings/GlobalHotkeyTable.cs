using ScreenPaste.Native;

namespace ScreenPaste.Settings;

/// <summary>What a global hotkey does (App runs it).</summary>
public enum GlobalHotkeyAction
{
    Capture,            // F1: snip
    Record,             // F2: record a region, or stop
    SnipAndCopy,        // Ctrl+F1: snip, and the chosen area goes straight to the clipboard
    CustomSnip,         // Shift+F1: a dialog sets the area and a delay
    Paste,              // F3: pin the clipboard, or bring back the pins closed last
    HidePins,           // Shift+F3: hide every pin, or show them again
    ClickThrough,       // the pin under the cursor lets clicks through, or no longer does

    // ---- More snip modes (unassigned by default)
    SnipAndPin,
    SnipCopyAndPin,
    LastAreaCopy,       // copy the area of the last snip again, at once
    FullScreenCopy,     // copy the whole screen, at once
    ActiveWindow,       // snip with the foreground window's area chosen
    ActiveScreen,       // snip with the monitor under the cursor chosen
    DelayedSnip,        // snip after AppSettings.SnipDelaySeconds
    ClearSnipHistory,   // forget the last areas (R) and the snip history (, .)
}

/// <summary>
/// One global hotkey: its RegisterHotKey id, what it does, its label (a Loc key), whether
/// the settings window lists it under "More hotkeys", and the settings property holding
/// its gesture ("" leaves it unassigned).
/// </summary>
public sealed record GlobalHotkey(
    int Id, GlobalHotkeyAction Action, string LabelKey, bool IsExtra,
    Func<AppSettings, string> Get, Action<AppSettings, string> Set);

/// <summary>An assigned global hotkey and what RegisterHotKey takes for it; null when the
/// gesture cannot be registered (the app reports it as failed).</summary>
public sealed record GlobalHotkeyBinding(GlobalHotkey Hotkey, string Gesture, (uint Modifiers, uint VirtualKey)? Win32);

/// <summary>
/// Every global hotkey as one table, so App registers them in one loop and the settings
/// window lists them from it. Each is registered on its own: one that fails (taken by
/// another app) leaves the others working.
/// </summary>
public static class GlobalHotkeyTable
{
    public static IReadOnlyList<GlobalHotkey> All { get; } = new[]
    {
        Row(0xB001, GlobalHotkeyAction.Capture, "set.capture", false, s => s.CaptureHotkey, (s, v) => s.CaptureHotkey = v),
        Row(0xB002, GlobalHotkeyAction.Record, "set.record", false, s => s.RecordHotkey, (s, v) => s.RecordHotkey = v),
        Row(0xB003, GlobalHotkeyAction.SnipAndCopy, "set.snipCopy", false, s => s.SnipAndCopyHotkey, (s, v) => s.SnipAndCopyHotkey = v),
        Row(0xB004, GlobalHotkeyAction.CustomSnip, "set.customSnip", false, s => s.CustomSnipHotkey, (s, v) => s.CustomSnipHotkey = v),
        Row(0xB005, GlobalHotkeyAction.Paste, "set.paste", false, s => s.PasteHotkey, (s, v) => s.PasteHotkey = v),
        Row(0xB006, GlobalHotkeyAction.HidePins, "set.hidePins", false, s => s.HidePinsHotkey, (s, v) => s.HidePinsHotkey = v),
        Row(0xB007, GlobalHotkeyAction.ClickThrough, "set.clickThrough", false, s => s.ClickThroughHotkey, (s, v) => s.ClickThroughHotkey = v),
        Row(0xB008, GlobalHotkeyAction.SnipAndPin, "set.snipPin", true, s => s.SnipAndPinHotkey, (s, v) => s.SnipAndPinHotkey = v),
        Row(0xB009, GlobalHotkeyAction.SnipCopyAndPin, "set.snipCopyPin", true, s => s.SnipCopyAndPinHotkey, (s, v) => s.SnipCopyAndPinHotkey = v),
        Row(0xB00A, GlobalHotkeyAction.LastAreaCopy, "set.lastAreaCopy", true, s => s.LastAreaCopyHotkey, (s, v) => s.LastAreaCopyHotkey = v),
        Row(0xB00B, GlobalHotkeyAction.FullScreenCopy, "set.fullScreenCopy", true, s => s.FullScreenCopyHotkey, (s, v) => s.FullScreenCopyHotkey = v),
        Row(0xB00C, GlobalHotkeyAction.ActiveWindow, "set.activeWindow", true, s => s.ActiveWindowHotkey, (s, v) => s.ActiveWindowHotkey = v),
        Row(0xB00D, GlobalHotkeyAction.ActiveScreen, "set.activeScreen", true, s => s.ActiveScreenHotkey, (s, v) => s.ActiveScreenHotkey = v),
        Row(0xB00E, GlobalHotkeyAction.DelayedSnip, "set.delayedSnip", true, s => s.DelayedSnipHotkey, (s, v) => s.DelayedSnipHotkey = v),
        Row(0xB00F, GlobalHotkeyAction.ClearSnipHistory, "set.clearSnipHistory", true, s => s.ClearSnipHistoryHotkey, (s, v) => s.ClearSnipHistoryHotkey = v),
    };

    public static GlobalHotkey For(GlobalHotkeyAction action) => All.First(h => h.Action == action);

    public static GlobalHotkey? ById(int id) => All.FirstOrDefault(h => h.Id == id);

    /// <summary>The assigned hotkeys in table order, with what RegisterHotKey takes for each.
    /// A blank gesture is unassigned and left out.</summary>
    public static IReadOnlyList<GlobalHotkeyBinding> Bindings(AppSettings settings) =>
        All.Select(h => (Hotkey: h, Gesture: (h.Get(settings) ?? "").Trim()))
           .Where(x => x.Gesture.Length > 0)
           .Select(x => new GlobalHotkeyBinding(x.Hotkey, x.Gesture, HotkeyGesture.ToWin32(x.Gesture)))
           .ToList();

    private static GlobalHotkey Row(int id, GlobalHotkeyAction action, string labelKey, bool isExtra,
        Func<AppSettings, string> get, Action<AppSettings, string> set) => new(id, action, labelKey, isExtra, get, set);
}
