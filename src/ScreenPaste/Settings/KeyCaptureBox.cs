using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Native;

namespace ScreenPaste.Settings;

/// <summary>
/// A read-only field that takes the next key pressed in it as an in-app shortcut
/// (<see cref="ShortcutKey"/>): a chord ("Ctrl+T", "R", "Esc", "Tab"), the character a symbol
/// key types ("[", "!", typed with Shift or AltGr as the layout needs), or a modifier tapped on
/// its own ("Shift"). Every key is taken, Tab, Esc and Delete too, so the field never moves the
/// focus or closes the window; its clear button empties it.
/// </summary>
internal sealed class KeyCaptureBox
{
    private static readonly ModifierKeys CommandModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;

    private ModifierKeys _tapArmed;        // a modifier went down alone and may be tapped
    private ShortcutKey? _pendingChord;    // a symbol key: its chord, should no character come
    private string? _pendingText;          // what that key types unshifted, for a dead key

    public KeyCaptureBox(ShortcutKey? value)
    {
        Box = SettingsWindow.Themed(new TextBox
        {
            IsReadOnly = true, IsUndoEnabled = false, IsReadOnlyCaretVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
        });
        InputMethod.SetIsInputMethodEnabled(Box, false);   // an IME would eat the keys
        Box.PreviewKeyDown += OnPreviewKeyDown;
        Box.PreviewKeyUp += OnPreviewKeyUp;
        Box.PreviewTextInput += OnPreviewTextInput;
        Box.PreviewMouseDown += (_, _) => _tapArmed = ModifierKeys.None;
        Box.LostKeyboardFocus += (_, _) => FlushPending();
        Show(value);
    }

    public TextBox Box { get; }

    public ShortcutKey? Value { get; private set; }

    /// <summary>The key was set by a press, cleared, or reset.</summary>
    public event Action? Changed;

    public void Set(ShortcutKey? value)
    {
        if (Value == value) return;
        Show(value);
        Changed?.Invoke();
    }

    private void Show(ShortcutKey? value)
    {
        Value = value;
        Box.Text = value is { } k ? ShortcutText.Key(k) : "";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        var modifiers = Keyboard.Modifiers;
        e.Handled = true;

        var modifier = ModifierOf(key);
        if (modifier != ModifierKeys.None)
        {
            if (!e.IsRepeat) _tapArmed = modifiers == modifier ? modifier : ModifierKeys.None;
            return;
        }
        _tapArmed = ModifierKeys.None;
        if (!KeyChord.IsChordKey(key)) return;

        if (((modifiers & CommandModifiers) == 0 && IsSymbolKey(key, modifiers)) || IsAltGr(modifiers))
        {
            // Wait for the character it types: what the layout makes of it (Shift+1 is "!",
            // AltGr+8 is "[" on a German layout). A Ctrl+Alt chord that types nothing stays a chord.
            _pendingChord = ShortcutKey.Chord(key, modifiers);
            _pendingText = KeyboardLayout.UnshiftedText(key);
            e.Handled = false;
            return;
        }
        FlushPending();
        Set(ShortcutKey.Chord(key, modifiers));
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
        if (_pendingChord is not { } chord) return;
        _pendingChord = null;
        Set(e.Text is { Length: 1 } text && ShortcutKey.IsCharacter(text[0]) ? ShortcutKey.Char(text[0]) : chord);
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifier = ModifierOf(key);
        if (modifier != ModifierKeys.None && modifier == _tapArmed)
        {
            _tapArmed = ModifierKeys.None;
            e.Handled = true;   // and Windows takes no Alt tap into menu mode
            Set(ShortcutKey.Tap(modifier));
            return;
        }
        FlushPending();
    }

    /// <summary>A symbol key no character came for (a dead key): its unshifted character, or
    /// the key itself.</summary>
    private void FlushPending()
    {
        if (_pendingChord is not { } chord) return;
        _pendingChord = null;
        Set(_pendingText is { Length: 1 } text && ShortcutKey.IsCharacter(text[0]) && chord.Modifiers == ModifierKeys.None
            ? ShortcutKey.Char(text[0])
            : chord);
    }

    /// <summary>A key bound by the character it types: the punctuation keys, the number pad's
    /// operators, and a digit with Shift (on a US layout Shift+1 is "!").</summary>
    private static bool IsSymbolKey(Key key, ModifierKeys modifiers) =>
        KeyChord.IsPunctuationKey(key) || key is Key.Add or Key.Subtract or Key.Multiply or Key.Divide ||
        (modifiers == ModifierKeys.Shift && key is >= Key.D0 and <= Key.D9);

    /// <summary>AltGr reaches WPF as Ctrl+Alt (with Shift for its shifted symbols).</summary>
    private static bool IsAltGr(ModifierKeys modifiers) =>
        (modifiers & CommandModifiers) == (ModifierKeys.Control | ModifierKeys.Alt);

    private static ModifierKeys ModifierOf(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None,
    };
}
