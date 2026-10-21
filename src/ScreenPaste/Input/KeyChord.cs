using System.Windows.Input;

namespace ScreenPaste.Input;

/// <summary>
/// A key plus the exact set of modifiers held with it, parsed from and written as the
/// human-friendly strings settings.json uses ("Ctrl+Shift+S", "F3", "R", "Shift+R").
///
/// Exists because WPF's <see cref="KeyGesture"/> refuses a bare letter or digit, while
/// Snipaste's in-app keys (R, C, 1 ...) are exactly that. Every string KeyGesture accepted
/// parses to the same key and modifiers here, so existing settings keep their meaning.
/// Pure (no dispatcher, no culture-dependent casing), so it is unit tested directly.
/// </summary>
public readonly record struct KeyChord(Key Key, ModifierKeys Modifiers)
{
    // Spellings KeyGesture's KeyConverter accepts besides the enum names.
    private static readonly Dictionary<string, Key> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Esc"] = Key.Escape,
        ["Enter"] = Key.Enter,
        ["Del"] = Key.Delete,
        ["Ins"] = Key.Insert,
        ["Backspace"] = Key.Back,
        ["Bksp"] = Key.Back,
        ["PgUp"] = Key.PageUp,
        ["PgDn"] = Key.PageDown,
        ["PrtSc"] = Key.PrintScreen,
        ["Semicolon"] = Key.OemSemicolon,
        ["Plus"] = Key.OemPlus,
        ["Comma"] = Key.OemComma,
        ["Minus"] = Key.OemMinus,
        ["Period"] = Key.OemPeriod,
        ["Question"] = Key.OemQuestion,
        ["Tilde"] = Key.OemTilde,
        ["OpenBrackets"] = Key.OemOpenBrackets,
        ["Pipe"] = Key.OemPipe,
        ["CloseBrackets"] = Key.OemCloseBrackets,
        ["Quotes"] = Key.OemQuotes,
        ["Backslash"] = Key.OemBackslash,
    };

    // Names for enum values that carry two (Return/Enter, Prior/PageUp ...), plus the
    // friendlier spellings the toolbar tooltips show.
    private static readonly Dictionary<Key, string> Names = new()
    {
        [Key.Escape] = "Esc",
        [Key.Enter] = "Enter",
        [Key.Back] = "Backspace",
        [Key.PageUp] = "PageUp",
        [Key.PageDown] = "PageDown",
        [Key.CapsLock] = "CapsLock",
        [Key.PrintScreen] = "PrintScreen",
    };

    /// <summary>Parse "Ctrl+Alt+C", "Shift+R", "1", "OemComma" ...; null when the text is
    /// empty, names an unknown key or modifier, or holds only modifiers.</summary>
    public static KeyChord? Parse(string? text) => TryParse(text, out var chord) ? chord : null;

    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text.Split('+');
        var modifiers = ModifierKeys.None;
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (ParseModifier(tokens[i].Trim()) is not { } m) return false;
            modifiers |= m;
        }

        if (ParseKey(tokens[^1].Trim()) is not { } key) return false;
        chord = new KeyChord(key, modifiers);
        return true;
    }

    /// <summary>Parse a user-configurable command gesture (UndoHotkey, CopyHotkey ...). Same as
    /// <see cref="Parse"/>, but keeps KeyGesture's rule that a letter or digit needs Ctrl, Alt
    /// or Win, so a hand-edited "C" in settings.json stays unbound as it always did instead
    /// of firing while the user works. Function keys, Delete, OemComma ... may stand alone.</summary>
    public static KeyChord? ParseGesture(string? text) =>
        Parse(text) is { } chord && (!IsLetterOrDigit(chord.Key) || HasCommandModifier(chord.Modifiers))
            ? chord
            : null;

    private static bool IsLetterOrDigit(Key key) => key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9;

    private static bool HasCommandModifier(ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;

    /// <summary>True when the pressed key and the held modifiers are exactly this chord
    /// (so Ctrl+Shift+Z never matches Ctrl+Z). Pass the effective key from
    /// <see cref="KeyInputNormalizer"/>.</summary>
    public bool Matches(Key key, ModifierKeys modifiers) => Key == key && Modifiers == modifiers;

    /// <summary>Whether <paramref name="key"/> can be the key of a chord: not None, not a
    /// modifier key and not one of WPF's System / ImeProcessed / DeadCharProcessed stand-ins.</summary>
    public static bool IsChordKey(Key key) => key is not (
        Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or
        Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin);

    /// <summary>A punctuation key (OemSemicolon ... OemBackslash), whose character depends on
    /// the keyboard layout.</summary>
    public static bool IsPunctuationKey(Key key) => key is >= Key.OemSemicolon and <= Key.OemBackslash;

    /// <summary>Modifiers in the order the settings hotkey editor writes them, then the key.</summary>
    public override string ToString() => Prefix() + KeyName(Key);

    /// <summary>As the settings windows show it: a punctuation key as the character
    /// <paramref name="keyText"/> says it types unshifted ("Shift+[" for Shift+OemOpenBrackets),
    /// anything else as <see cref="ToString"/>.</summary>
    public string ToDisplayString(Func<Key, string?> keyText) =>
        IsPunctuationKey(Key) && keyText(Key) is { Length: 1 } text ? Prefix() + text : ToString();

    private string Prefix()
    {
        var prefix = "";
        if (Modifiers.HasFlag(ModifierKeys.Control)) prefix += "Ctrl+";
        if (Modifiers.HasFlag(ModifierKeys.Alt)) prefix += "Alt+";
        if (Modifiers.HasFlag(ModifierKeys.Shift)) prefix += "Shift+";
        if (Modifiers.HasFlag(ModifierKeys.Windows)) prefix += "Win+";
        return prefix;
    }

    private static ModifierKeys? ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => ModifierKeys.Control,
        "SHIFT" => ModifierKeys.Shift,
        "ALT" => ModifierKeys.Alt,
        "WIN" or "WINDOWS" => ModifierKeys.Windows,
        _ => null,
    };

    private static Key? ParseKey(string token)
    {
        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= '0' and <= '9') return Key.D0 + (c - '0');
            if (c is >= 'A' and <= 'Z') return Key.A + (c - 'A');
            return null;
        }

        if (Aliases.TryGetValue(token, out var alias)) return alias;

        // Enum.TryParse also accepts numbers ("12") and comma lists ("A,B"); an enum name is
        // letters and digits starting with a letter, so anything else is rejected up front.
        if (token.Length == 0 || !char.IsAsciiLetter(token[0]) || !token.All(char.IsAsciiLetterOrDigit)) return null;
        if (!Enum.TryParse<Key>(token, ignoreCase: true, out var key) || !Enum.IsDefined(key)) return null;
        return IsChordKey(key) ? key : null;
    }

    private static string KeyName(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
        return Names.TryGetValue(key, out var name) ? name : key.ToString();
    }
}
