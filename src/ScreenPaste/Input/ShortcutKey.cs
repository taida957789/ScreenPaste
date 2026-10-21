using System.Windows.Input;

namespace ScreenPaste.Input;

/// <summary>What a <see cref="ShortcutKey"/> listens to.</summary>
public enum ShortcutKeyKind
{
    /// <summary>A key with exactly these modifiers ("Ctrl+T", "R", "Shift+R").</summary>
    Chord,
    /// <summary>The character a key types, wherever the keyboard layout puts it ("[", "!").</summary>
    Character,
    /// <summary>A modifier pressed and released on its own ("Shift": the loupe's format).</summary>
    Tap,
}

/// <summary>One key event an in-app action can be bound to, as <see cref="ShortcutKey.Matches"/> sees it.</summary>
public readonly record struct KeyInput
{
    /// <summary>The effective key pressed (<see cref="KeyInputNormalizer"/>); None for typing or a tap.</summary>
    public Key Key { get; init; }
    public ModifierKeys Modifiers { get; init; }
    /// <summary>What the pressed key types on the current layout with no modifier held, or null.</summary>
    public string? LayoutText { get; init; }
    /// <summary>The character typed (TextInput), or null for a key press or a tap.</summary>
    public string? Text { get; init; }
    /// <summary>The modifier tapped on its own (<see cref="ModifierTapTracker"/>), or None.</summary>
    public ModifierKeys Tap { get; init; }

    public static KeyInput Press(Key key, ModifierKeys modifiers, string? layoutText = null) =>
        new() { Key = key, Modifiers = modifiers, LayoutText = layoutText };

    public static KeyInput Typed(string text) => new() { Text = text };

    public static KeyInput Tapped(ModifierKeys modifier) => new() { Tap = modifier };
}

/// <summary>
/// A key an in-app action of the capture or a pin listens to, written in settings.json as
/// "Ctrl+T", "R", "1" (a chord), "[", "!", "Ctrl++" (a character) or "Shift" (a tap). Named
/// so as not to clash with WPF's KeyBinding.
///
/// Characters are for the keys whose place depends on the keyboard layout. One with no
/// modifier matches the character typed, whatever typed it (Shift, or AltGr on many European
/// layouts), and the key whose unshifted character it is; one with Ctrl, Alt or Win matches
/// that key with them, Shift or not. A top-row digit also matches the number pad's.
/// Pure, so it is unit tested.
/// </summary>
public readonly record struct ShortcutKey
{
    // static readonly, not const: coverlet cannot resolve WindowsBase enums in constants.
    private static readonly ModifierKeys CommandModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;

    private ShortcutKey(ShortcutKeyKind kind, Key key, char character, ModifierKeys modifiers)
    {
        Kind = kind;
        Key = key;
        Character = character;
        Modifiers = modifiers;
    }

    public ShortcutKeyKind Kind { get; }
    /// <summary>A chord's key; None otherwise.</summary>
    public Key Key { get; }
    /// <summary>A character binding's character; '\0' otherwise.</summary>
    public char Character { get; }
    /// <summary>A chord's exact modifiers, a character's Ctrl/Alt/Win, or the tapped modifier.</summary>
    public ModifierKeys Modifiers { get; }

    public static ShortcutKey Chord(Key key, ModifierKeys modifiers) => new(ShortcutKeyKind.Chord, key, '\0', modifiers);

    public static ShortcutKey Char(char character, ModifierKeys modifiers = ModifierKeys.None) =>
        new(ShortcutKeyKind.Character, Key.None, character, modifiers & CommandModifiers);

    public static ShortcutKey Tap(ModifierKeys modifier) => new(ShortcutKeyKind.Tap, Key.None, '\0', modifier);

    /// <summary>Holds Ctrl, Alt or Win, so it can run while a text annotation is typed
    /// without ever firing mid-word.</summary>
    public bool HasCommandModifier => Kind != ShortcutKeyKind.Tap && (Modifiers & CommandModifiers) != 0;

    /// <summary>A symbol that can only be a character binding: not a letter, digit or space.</summary>
    public static bool IsCharacter(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);

    /// <summary>Read a binding from settings.json; null when no key can be it.</summary>
    public static ShortcutKey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.Length == 1) return IsCharacter(text[0]) ? Char(text[0]) : ChordOf(text);
        if (ParseModifier(text) is { } tapped) return Tap(tapped);

        // "Ctrl++": the character is the last '+', and the one before it joins the modifiers.
        if (text.EndsWith("++", StringComparison.Ordinal)) return CharacterWith(text[..^2], '+');
        if (text[^2] == '+' && IsCharacter(text[^1])) return CharacterWith(text[..^2], text[^1]);
        return ChordOf(text);
    }

    public bool Matches(KeyInput input) => Kind switch
    {
        ShortcutKeyKind.Chord => input.Key != Key.None && SameKey(Key, input.Key) && Modifiers == input.Modifiers,
        ShortcutKeyKind.Character => MatchesCharacter(input),
        _ => input.Tap != ModifierKeys.None && input.Tap == Modifiers,
    };

    /// <summary>Whether a key event could match both: the settings window's clash check.</summary>
    public bool Collides(ShortcutKey other)
    {
        if (Kind != other.Kind || Modifiers != other.Modifiers) return false;
        return Kind switch
        {
            ShortcutKeyKind.Chord => SameKey(Key, other.Key) || SameKey(other.Key, Key),
            ShortcutKeyKind.Character => Character == other.Character,
            _ => true,
        };
    }

    /// <summary>As the settings windows show it: a symbol with no
    /// modifier that the layout types with Shift reads as that Shift chord ("Shift+1" for "!",
    /// "Shift+[" for "{"), the key named by <paramref name="shiftedKeyOf"/>; a chord on a
    /// punctuation key names it by its character, as <paramref name="keyText"/> gives it
    /// (<see cref="KeyChord.ToDisplayString"/>); anything else as <see cref="ToString"/>.</summary>
    public string ToDisplayString(Func<char, string?> shiftedKeyOf, Func<Key, string?> keyText) => Kind switch
    {
        ShortcutKeyKind.Chord => new KeyChord(Key, Modifiers).ToDisplayString(keyText),
        ShortcutKeyKind.Character when Modifiers == ModifierKeys.None && shiftedKeyOf(Character) is { } key =>
            Prefix(ModifierKeys.Shift) + key,
        _ => ToString(),
    };

    /// <summary>As settings.json writes it; <see cref="Parse"/> reads it back.</summary>
    public override string ToString() => Kind switch
    {
        ShortcutKeyKind.Chord => new KeyChord(Key, Modifiers).ToString(),
        ShortcutKeyKind.Character => Prefix(Modifiers) + Character,
        _ => ModifierName(Modifiers),
    };

    private bool MatchesCharacter(KeyInput input)
    {
        if (input.Text != null)
            return (Modifiers & CommandModifiers) == 0 && input.Text.Length == 1 && input.Text[0] == Character;
        var typed = input.LayoutText ?? NumberPadCharacter(input.Key);
        return input.Key != Key.None && typed is { Length: 1 } && typed[0] == Character &&
               (input.Modifiers & ~ModifierKeys.Shift) == Modifiers;
    }

    /// <summary>What the number pad's operator keys type, on every layout.</summary>
    private static string? NumberPadCharacter(Key key) => key switch
    {
        Key.Add => "+",
        Key.Subtract => "-",
        Key.Multiply => "*",
        Key.Divide => "/",
        _ => null,
    };

    /// <summary>The bound key itself, or the number pad's twin of a bound top-row digit.</summary>
    private static bool SameKey(Key bound, Key pressed) =>
        bound == pressed || (bound is >= Key.D0 and <= Key.D9 && pressed == Key.NumPad0 + (bound - Key.D0));

    private static ShortcutKey? ChordOf(string text) =>
        KeyChord.Parse(text) is { } chord ? Chord(chord.Key, chord.Modifiers) : null;

    /// <summary>A character with Ctrl, Alt or Win before it; Shift is part of typing it.</summary>
    private static ShortcutKey? CharacterWith(string modifierText, char character)
    {
        var modifiers = ModifierKeys.None;
        foreach (var token in modifierText.Split('+'))
        {
            if (ParseModifier(token.Trim()) is not { } m) return null;
            modifiers |= m;
        }
        if (modifiers.HasFlag(ModifierKeys.Shift) || (modifiers & CommandModifiers) == 0) return null;
        return Char(character, modifiers);
    }

    private static ModifierKeys? ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => ModifierKeys.Control,
        "SHIFT" => ModifierKeys.Shift,
        "ALT" => ModifierKeys.Alt,
        "WIN" or "WINDOWS" => ModifierKeys.Windows,
        _ => null,
    };

    private static string Prefix(ModifierKeys modifiers)
    {
        var prefix = "";
        if (modifiers.HasFlag(ModifierKeys.Control)) prefix += "Ctrl+";
        if (modifiers.HasFlag(ModifierKeys.Alt)) prefix += "Alt+";
        if (modifiers.HasFlag(ModifierKeys.Shift)) prefix += "Shift+";
        if (modifiers.HasFlag(ModifierKeys.Windows)) prefix += "Win+";
        return prefix;
    }

    private static string ModifierName(ModifierKeys modifier) => modifier switch
    {
        ModifierKeys.Control => "Ctrl",
        ModifierKeys.Alt => "Alt",
        ModifierKeys.Windows => "Win",
        _ => "Shift",
    };
}
