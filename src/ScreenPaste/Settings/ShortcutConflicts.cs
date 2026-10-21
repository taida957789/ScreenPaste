using ScreenPaste.Input;

namespace ScreenPaste.Settings;

public enum ShortcutConflictKind
{
    /// <summary>Two global hotkeys: only the first one registers.</summary>
    GlobalDuplicate,
    /// <summary>A global hotkey on an in-app key: Windows hands it to the hotkey, so the
    /// capture and the pins never see it.</summary>
    GlobalTakesInAppKey,
    /// <summary>Two in-app actions on one key where both can run: only the first in the
    /// key router's order does.</summary>
    InAppDuplicate,
}

/// <summary>One key used more than once, and the names of what uses it.</summary>
public sealed record ShortcutConflict(ShortcutConflictKind Kind, string Key, IReadOnlyList<string> Names);

/// <summary>
/// Finds keys used twice, for the settings windows' warnings: among the global hotkeys,
/// between a global hotkey and an in-app key (any context: Windows takes a registered hotkey
/// everywhere), and between two in-app actions of the capture or of a pin that can run at the
/// same time (<see cref="ShortcutContexts"/>: Space framing and Space annotating are no
/// clash). Gestures are compared as keys, so "Shift+Ctrl+A" is "Ctrl+Shift+A". Pure, so it is
/// unit tested.
/// </summary>
public static class ShortcutConflicts
{
    /// <summary>The global hotkeys' clashes, among themselves and with <paramref name="inApp"/>.</summary>
    /// <param name="global">Each global hotkey's name and gesture; blank or unreadable ones take part in nothing.</param>
    /// <param name="actionName">An in-app action's name as the warning shows it.</param>
    public static IReadOnlyList<ShortcutConflict> Global(
        IEnumerable<(string Name, string? Gesture)> global, ShortcutMap inApp, Func<ShortcutAction, string> actionName)
    {
        var conflicts = new List<ShortcutConflict>();
        var hotkeys = global
            .Select(g => (g.Name, Chord: KeyChord.Parse(g.Gesture)))
            .Where(g => g.Chord != null)
            .GroupBy(g => g.Chord!.Value);
        foreach (var group in hotkeys)
        {
            var key = ShortcutKey.Chord(group.Key.Key, group.Key.Modifiers);
            var names = group.Select(g => g.Name).ToList();
            if (names.Count > 1)
            {
                conflicts.Add(new ShortcutConflict(ShortcutConflictKind.GlobalDuplicate, key.ToString(), names));
                continue;
            }
            var taken = ShortcutActions.All.Where(a => inApp.For(a.Id).Any(k => k.Collides(key))).Select(actionName).ToList();
            if (taken.Count > 0)
                conflicts.Add(new ShortcutConflict(ShortcutConflictKind.GlobalTakesInAppKey, key.ToString(), names.Concat(taken).ToList()));
        }
        return conflicts;
    }

    /// <summary>Two in-app actions on one key in the same area and a shared context, each key
    /// named once with every action on it, in table order.</summary>
    public static IReadOnlyList<ShortcutConflict> InApp(ShortcutMap map, Func<ShortcutAction, string>? actionName = null)
    {
        var bound = ShortcutActions.All.SelectMany(a => map.For(a.Id).Select(k => (Action: a, Key: k))).ToList();
        var clashes = new Dictionary<string, List<ShortcutAction>>();
        for (int i = 0; i < bound.Count; i++)
        for (int j = i + 1; j < bound.Count; j++)
        {
            var (a, ka) = bound[i];
            var (b, kb) = bound[j];
            if (a == b || a.Area != b.Area || (a.Contexts & b.Contexts) == 0 || !ka.Collides(kb)) continue;
            var key = ka.ToString();
            if (!clashes.TryGetValue(key, out var actions)) clashes[key] = actions = new();
            if (!actions.Contains(a)) actions.Add(a);
            if (!actions.Contains(b)) actions.Add(b);
        }
        var name = actionName ?? (a => a.Id);
        return clashes
            .Select(c => new ShortcutConflict(ShortcutConflictKind.InAppDuplicate, c.Key, c.Value.Select(name).ToList()))
            .ToList();
    }

    /// <summary>Every clash in <paramref name="settings"/>; <paramref name="label"/> turns a Loc
    /// key into text (Loc.T in the app) and <paramref name="actionName"/> names an in-app action.</summary>
    public static IReadOnlyList<ShortcutConflict> ForSettings(
        AppSettings settings, Func<string, string> label, Func<ShortcutAction, string> actionName)
    {
        var map = ShortcutMap.FromSettings(settings);
        return Global(GlobalHotkeyTable.All.Select(h => (label(h.LabelKey), (string?)h.Get(settings))), map, actionName)
            .Concat(InApp(map, actionName))
            .ToList();
    }
}
