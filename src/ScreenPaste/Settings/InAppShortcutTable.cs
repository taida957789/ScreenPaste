namespace ScreenPaste.Settings;

/// <summary>A configurable in-app gesture: its label (a Loc key) and the settings property
/// holding it ("" leaves it unbound).</summary>
public sealed record InAppShortcut(string LabelKey, Func<AppSettings, string> Get, Action<AppSettings, string> Set);

/// <summary>
/// The in-app gestures the settings window lets the user change (the capture overlay and
/// the pins read them through OverlayKeyBindings / PinKeyBindings). Snipaste's other in-app
/// keys are fixed rows of the key routers and are not rebindable.
/// </summary>
public static class InAppShortcutTable
{
    public static IReadOnlyList<InAppShortcut> All { get; } = new InAppShortcut[]
    {
        new("action.undo", s => s.UndoHotkey, (s, v) => s.UndoHotkey = v),
        new("action.redo", s => s.RedoHotkey, (s, v) => s.RedoHotkey = v),
        new("action.copy", s => s.CopyHotkey, (s, v) => s.CopyHotkey = v),
        new("action.save", s => s.SaveHotkey, (s, v) => s.SaveHotkey = v),
        new("set.quickSave", s => s.QuickSaveHotkey, (s, v) => s.QuickSaveHotkey = v),
        new("action.pin", s => s.PinHotkey, (s, v) => s.PinHotkey = v),
        new("set.print", s => s.PrintHotkey, (s, v) => s.PrintHotkey = v),
        new("action.clearAll", s => s.ClearAllHotkey, (s, v) => s.ClearAllHotkey = v),
    };
}
