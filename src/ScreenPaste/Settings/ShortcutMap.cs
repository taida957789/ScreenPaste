using ScreenPaste.Input;

namespace ScreenPaste.Settings;

/// <summary>
/// The keys of every in-app action (<see cref="ShortcutActions"/>) as the user has them: the
/// defaults, with the lists settings.json holds in <see cref="AppSettings.KeyBindings"/> in
/// their place. An action with no list there but an old single-gesture setting (CopyHotkey
/// ...) has that gesture in place of its first default, so settings written before every key
/// was rebindable keep their meaning. Immutable and pure, so it is unit tested; the key
/// routers ask it whether a key event is an action's.
/// </summary>
public sealed class ShortcutMap
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ShortcutKey>> _keys;

    private ShortcutMap(IReadOnlyDictionary<string, IReadOnlyList<ShortcutKey>> keys) => _keys = keys;

    public static ShortcutMap Defaults { get; } = new(ShortcutActions.All.ToDictionary(a => a.Id, a => a.DefaultKeys));

    public static ShortcutMap FromSettings(AppSettings settings)
    {
        var stored = settings.KeyBindings ?? new();
        return new(ShortcutActions.All.ToDictionary(a => a.Id, a => KeysFor(a, settings, stored)));
    }

    /// <summary>The keys of <paramref name="id"/>; none for an unbound or unknown action.</summary>
    public IReadOnlyList<ShortcutKey> For(string id) =>
        _keys.TryGetValue(id, out var keys) ? keys : Array.Empty<ShortcutKey>();

    public bool Matches(string id, KeyInput input)
    {
        foreach (var key in For(id))
            if (key.Matches(input)) return true;
        return false;
    }

    /// <summary>A copy with <paramref name="id"/> bound to <paramref name="keys"/> (none: unbound).</summary>
    public ShortcutMap With(string id, IEnumerable<ShortcutKey> keys)
    {
        var copy = new Dictionary<string, IReadOnlyList<ShortcutKey>>(_keys) { [id] = keys.Distinct().ToArray() };
        return new(copy);
    }

    /// <summary><see cref="With"/> from settings-style text, unreadable keys skipped.</summary>
    public ShortcutMap WithKeys(string id, params string[] keys) =>
        With(id, keys.Select(ShortcutKey.Parse).OfType<ShortcutKey>());

    /// <summary>"Ctrl+C / Ctrl+Insert" for a tooltip or a menu; "" when unbound.
    /// <paramref name="describe"/> turns one key into text (the settings window spells out taps).</summary>
    public string Label(string id, Func<ShortcutKey, string>? describe = null) =>
        string.Join(" / ", For(id).Select(k => describe?.Invoke(k) ?? k.ToString()));

    /// <summary>Keep the old single-gesture settings in step with the capture's first chord
    /// (the recording editor still reads them), then write into settings.json's list every
    /// action that reading the file back would otherwise get wrong: one changed from its
    /// defaults, or a pin output whose old gesture the capture's change just moved. Anything
    /// taken back to the default is forgotten; lists of actions this version does not know (a
    /// newer one wrote them) are kept.</summary>
    public void SaveTo(AppSettings settings)
    {
        foreach (var action in ShortcutActions.All)
            if (action.LegacySet is { } set) set(settings, FirstChord(For(action.Id)));

        var none = new Dictionary<string, List<string>>();
        var changed = (settings.KeyBindings ?? none)
            .Where(stored => ShortcutActions.ById(stored.Key) == null)
            .ToDictionary(stored => stored.Key, stored => stored.Value);
        foreach (var action in ShortcutActions.All)
        {
            var keys = For(action.Id);
            if (!keys.SequenceEqual(KeysFor(action, settings, none))) changed[action.Id] = keys.Select(k => k.ToString()).ToList();
        }
        settings.KeyBindings = changed;
    }

    private static string FirstChord(IReadOnlyList<ShortcutKey> keys)
    {
        foreach (var key in keys)
            if (key.Kind == ShortcutKeyKind.Chord) return key.ToString();
        return "";
    }

    private static IReadOnlyList<ShortcutKey> KeysFor(
        ShortcutAction action, AppSettings settings, IReadOnlyDictionary<string, List<string>> stored)
    {
        if (stored.TryGetValue(action.Id, out var list) && list != null)
            return list.Select(ShortcutKey.Parse).OfType<ShortcutKey>().Distinct().ToArray();
        if (action.LegacyGet is not { } get) return action.DefaultKeys;

        // The old gesture (read as it always was: a bare letter stays unbound) stands in for
        // the first default; the others (Ctrl+Insert for Copy) stay.
        var rest = action.DefaultKeys.Skip(1);
        var legacy = KeyChord.ParseGesture(get(settings));
        var keys = legacy is { } c ? rest.Prepend(ShortcutKey.Chord(c.Key, c.Modifiers)) : rest;
        return keys.Distinct().ToArray();
    }
}
