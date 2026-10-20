using ScreenPaste.Capture;
using ScreenPaste.Input;
using ScreenPaste.Output;

namespace ScreenPaste.Settings;

/// <summary>Where a gesture works: system-wide, in the capture and on pins (configurable), or
/// one of the key routers' fixed keys.</summary>
public enum ShortcutScope { Global, InApp, BuiltIn }

/// <summary>A named gesture; a blank or unreadable gesture takes part in nothing.</summary>
public readonly record struct ShortcutEntry(string Name, string? Gesture, ShortcutScope Scope);

public enum ShortcutConflictKind
{
    /// <summary>Two global hotkeys: only the first one registers.</summary>
    GlobalDuplicate,
    /// <summary>A global hotkey on an in-app key: Windows hands it to the hotkey, so the
    /// capture and the pins never see it.</summary>
    GlobalTakesInAppKey,
    /// <summary>Two in-app gestures: only the first in the router's order runs.</summary>
    InAppDuplicate,
    /// <summary>An in-app gesture on a fixed key: the gesture wins, the fixed key is lost.</summary>
    InAppReplacesBuiltIn,
}

/// <summary>One chord used more than once; <see cref="Names"/> are the global and in-app
/// entries that use it (fixed keys have no name of their own).</summary>
public sealed record ShortcutConflict(ShortcutConflictKind Kind, KeyChord Chord, IReadOnlyList<string> Names);

/// <summary>
/// Finds gestures used twice, for the settings window's warning: among the global hotkeys,
/// between a global hotkey and an in-app key, among the in-app gestures, and between an
/// in-app gesture and a fixed key of the capture or the pins. Gestures are compared as
/// <see cref="KeyChord"/>s, so "Shift+Ctrl+A" is "Ctrl+Shift+A". Pure, so it is unit tested.
/// </summary>
public static class ShortcutConflicts
{
    public static IReadOnlyList<ShortcutConflict> Find(IEnumerable<ShortcutEntry> entries)
    {
        var conflicts = new List<ShortcutConflict>();
        var groups = entries
            .Select(e => (Entry: e, Chord: KeyChord.Parse(e.Gesture)))
            .Where(x => x.Chord != null)
            .GroupBy(x => x.Chord!.Value);
        foreach (var group in groups)
        {
            var scopes = group.Select(x => x.Entry.Scope).ToList();
            if (KindOf(scopes) is not { } kind) continue;
            var names = group.Where(x => x.Entry.Scope != ShortcutScope.BuiltIn).Select(x => x.Entry.Name).ToList();
            conflicts.Add(new ShortcutConflict(kind, group.Key, names));
        }
        return conflicts;
    }

    /// <summary>The conflicts in <paramref name="settings"/>, the entries named by
    /// <paramref name="label"/> (Loc.T in the app) from their Loc keys.</summary>
    public static IReadOnlyList<ShortcutConflict> ForSettings(AppSettings settings, Func<string, string> label) =>
        Find(Entries(
            GlobalHotkeyTable.All.Select(h => (label(h.LabelKey), (string?)h.Get(settings))),
            InAppShortcutTable.All.Select(s => (label(s.LabelKey), (string?)s.Get(settings)))));

    /// <summary>The global and in-app gestures given, plus every fixed key.</summary>
    public static IEnumerable<ShortcutEntry> Entries(
        IEnumerable<(string Name, string? Gesture)> global, IEnumerable<(string Name, string? Gesture)> inApp) =>
        global.Select(g => new ShortcutEntry(g.Name, g.Gesture, ShortcutScope.Global))
            .Concat(inApp.Select(i => new ShortcutEntry(i.Name, i.Gesture, ShortcutScope.InApp)))
            .Concat(BuiltIns());

    /// <summary>The fixed chords of the capture overlay and of a pin.</summary>
    public static IEnumerable<ShortcutEntry> BuiltIns() =>
        OverlayKeyRouter.FixedChords.Concat(PinKeyRouter.FixedChords)
            .Select(c => new ShortcutEntry(c.ToString(), c.ToString(), ShortcutScope.BuiltIn));

    private static ShortcutConflictKind? KindOf(IReadOnlyList<ShortcutScope> scopes)
    {
        int global = scopes.Count(s => s == ShortcutScope.Global);
        int inApp = scopes.Count(s => s == ShortcutScope.InApp);
        if (global >= 2) return ShortcutConflictKind.GlobalDuplicate;
        if (global == 1) return scopes.Count > 1 ? ShortcutConflictKind.GlobalTakesInAppKey : null;
        if (inApp >= 2) return ShortcutConflictKind.InAppDuplicate;
        if (inApp == 1) return scopes.Count > 1 ? ShortcutConflictKind.InAppReplacesBuiltIn : null;
        return null;   // fixed keys alone: each context has its own
    }
}
