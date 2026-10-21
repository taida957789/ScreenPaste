using System.Windows.Input;

namespace ScreenPaste.Input;

/// <summary>
/// The key the user actually pressed. WPF reports Alt chords and F10 as
/// <see cref="Key.System"/>, keys an IME looked at as <see cref="Key.ImeProcessed"/> (so a
/// Chinese or Japanese IME turns C into ImeProcessed), and dead keys as
/// <see cref="Key.DeadCharProcessed"/>, each with the real key in a side property.
/// KeyGesture.Matches unwraps these internally; key tables built on <see cref="KeyChord"/>
/// need it done explicitly.
/// </summary>
public static class KeyInputNormalizer
{
    // Overloads rather than an optional `Key deadCharProcessedKey = Key.None`: coverlet cannot
    // resolve WindowsBase enums in parameter defaults and would skip instrumenting the app.
    public static Key Effective(Key key, Key systemKey, Key imeProcessedKey) =>
        Effective(key, systemKey, imeProcessedKey, Key.None);

    public static Key Effective(Key key, Key systemKey, Key imeProcessedKey,
        Key deadCharProcessedKey) => key switch
    {
        Key.System => systemKey,
        Key.ImeProcessed => imeProcessedKey,
        Key.DeadCharProcessed => deadCharProcessedKey,
        _ => key,
    };

    public static Key Effective(KeyEventArgs e) =>
        Effective(e.Key, e.SystemKey, e.ImeProcessedKey, e.DeadCharProcessedKey);
}
