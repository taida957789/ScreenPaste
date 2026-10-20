using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The key + modifier pair the overlay matches shortcuts against. Unlike WPF's KeyGesture it
/// can hold a bare letter or digit, which Snipaste's in-app keys (R, 1, C ...) need.
/// </summary>
public sealed class KeyChordTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Win = ModifierKeys.Windows;

    // ---------------------------------------------------------------- parse ---

    [Theory]
    [InlineData("R", Key.R, ModifierKeys.None)]
    [InlineData("r", Key.R, ModifierKeys.None)]
    [InlineData("1", Key.D1, ModifierKeys.None)]
    [InlineData("0", Key.D0, ModifierKeys.None)]
    [InlineData("Shift+R", Key.R, Shift)]
    [InlineData("OemComma", Key.OemComma, ModifierKeys.None)]
    [InlineData("Ctrl+Insert", Key.Insert, Ctrl)]
    [InlineData("Ctrl+Alt+C", Key.C, Ctrl | Alt)]
    [InlineData(" ctrl + shift + s ", Key.S, Ctrl | Shift)]
    [InlineData("Control+Z", Key.Z, Ctrl)]
    [InlineData("Win+Shift+S", Key.S, Win | Shift)]
    [InlineData("Esc", Key.Escape, ModifierKeys.None)]
    [InlineData("Escape", Key.Escape, ModifierKeys.None)]
    [InlineData("Enter", Key.Enter, ModifierKeys.None)]
    [InlineData("Del", Key.Delete, ModifierKeys.None)]
    [InlineData("F3", Key.F3, ModifierKeys.None)]
    [InlineData("Shift+F3", Key.F3, Shift)]
    [InlineData("Ctrl+Comma", Key.OemComma, Ctrl)]
    public void Parse_ReadsTheKeyAndItsModifiers(string text, Key key, ModifierKeys modifiers)
    {
        var chord = KeyChord.Parse(text);

        Assert.Equal(new KeyChord(key, modifiers), chord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl++")]
    [InlineData("Ctrl")]                // a modifier on its own is not a chord
    [InlineData("Ctrl+Shift")]
    [InlineData("Hyper+A")]             // unknown modifier
    [InlineData("Ctrl+Z+Y")]            // Z is not a modifier
    [InlineData("12")]                  // Enum.TryParse would read this as (Key)12
    [InlineData("-1")]
    [InlineData("NotAKey")]
    [InlineData("A,B")]                 // Enum.TryParse would OR these together
    [InlineData("ImeProcessed")]        // pseudo keys never arrive as a real key
    public void Parse_ReturnsNull_ForTextThatIsNotAChord(string? text)
    {
        Assert.Null(KeyChord.Parse(text));
    }

    [Fact]
    public void TryParse_ReportsSuccessAndTheChord()
    {
        bool ok = KeyChord.TryParse("Ctrl+Shift+Z", out var chord);

        Assert.True(ok);
        Assert.Equal(new KeyChord(Key.Z, Ctrl | Shift), chord);
    }

    /// <summary>Settings files written before KeyChord existed hold KeyGesture strings; every
    /// one that parsed then must mean the same chord now.</summary>
    [Theory]
    [InlineData("Ctrl+Z")]
    [InlineData("Ctrl+Y")]
    [InlineData("Ctrl+C")]
    [InlineData("Ctrl+S")]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("F1")]
    [InlineData("F2")]
    [InlineData("Ctrl+Shift+A")]
    [InlineData("Ctrl+Alt+Shift+F5")]
    [InlineData("Control+Shift+Z")]
    [InlineData("Ctrl+OemComma")]
    [InlineData("Ctrl+Plus")]
    [InlineData("Ctrl+Minus")]
    [InlineData("Ctrl+Insert")]
    [InlineData("Shift+F3")]
    [InlineData("Ctrl+F1")]
    [InlineData("Delete")]
    [InlineData("Ctrl+Enter")]
    [InlineData("Ctrl+Back")]
    [InlineData("Alt+PageDown")]
    public void Parse_AgreesWithKeyGesture_ForStringsThatAlreadyParse(string text)
    {
        var gesture = HotkeyGesture.Parse(text);
        Assert.NotNull(gesture);   // precondition: a string the old parser accepted

        var chord = KeyChord.Parse(text);

        Assert.Equal(new KeyChord(gesture!.Key, gesture.Modifiers), chord);
    }

    // ---------------------------------------------------------------- gesture ---

    /// <summary>The configurable gestures (UndoHotkey ...) keep KeyGesture's rule: a letter or
    /// digit needs Ctrl, Alt or Win, so a hand-edited "C" cannot fire while the user works.</summary>
    [Theory]
    [InlineData("C")]
    [InlineData("z")]
    [InlineData("1")]
    [InlineData("Shift+C")]
    [InlineData("Shift+1")]
    [InlineData("")]
    [InlineData("NotAKey")]
    public void ParseGesture_RejectsWhatKeyGestureRejected(string text)
    {
        Assert.Null(HotkeyGesture.Parse(text));   // precondition: the old parser refused it

        Assert.Null(KeyChord.ParseGesture(text));
    }

    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Alt+1")]
    [InlineData("Win+Shift+S")]
    [InlineData("Ctrl+Shift+Z")]
    [InlineData("F3")]
    [InlineData("Shift+F3")]
    [InlineData("Delete")]
    [InlineData("Insert")]
    [InlineData("NumPad1")]
    [InlineData("Shift+OemComma")]
    public void ParseGesture_AcceptsWhatKeyGestureAccepted(string text)
    {
        var gesture = HotkeyGesture.Parse(text);
        Assert.NotNull(gesture);

        var chord = KeyChord.ParseGesture(text);

        Assert.Equal(new KeyChord(gesture!.Key, gesture.Modifiers), chord);
    }

    // ---------------------------------------------------------------- format ---

    [Theory]
    [InlineData(Key.R, ModifierKeys.None, "R")]
    [InlineData(Key.D1, ModifierKeys.None, "1")]
    [InlineData(Key.R, Shift, "Shift+R")]
    [InlineData(Key.OemComma, ModifierKeys.None, "OemComma")]
    [InlineData(Key.Insert, Ctrl, "Ctrl+Insert")]
    [InlineData(Key.C, Ctrl | Alt, "Ctrl+Alt+C")]
    [InlineData(Key.S, Shift | Ctrl, "Ctrl+Shift+S")]
    [InlineData(Key.F5, Win | Shift | Alt | Ctrl, "Ctrl+Alt+Shift+Win+F5")]
    [InlineData(Key.Escape, ModifierKeys.None, "Esc")]
    [InlineData(Key.Enter, ModifierKeys.None, "Enter")]
    [InlineData(Key.PageDown, ModifierKeys.None, "PageDown")]
    [InlineData(Key.Back, Ctrl, "Ctrl+Backspace")]
    public void ToString_WritesModifiersInTheSettingsEditorOrder(Key key, ModifierKeys modifiers, string expected)
    {
        var text = new KeyChord(key, modifiers).ToString();

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("R")]
    [InlineData("1")]
    [InlineData("Shift+R")]
    [InlineData("OemComma")]
    [InlineData("Ctrl+Insert")]
    [InlineData("Ctrl+Alt+C")]
    [InlineData("Ctrl+Shift+Z")]
    [InlineData("Esc")]
    [InlineData("Enter")]
    [InlineData("F12")]
    public void ParseThenFormat_RoundTrips(string text)
    {
        var chord = KeyChord.Parse(text);

        Assert.NotNull(chord);
        Assert.Equal(text, chord!.Value.ToString());
    }

    /// <summary>Every real key survives a format/parse cycle, including the ones whose enum
    /// value has two names (Enter/Return, PageUp/Prior ...).</summary>
    [Fact]
    public void FormatThenParse_RoundTrips_ForEveryRealKey()
    {
        var failures = new List<string>();
        foreach (var key in Enum.GetValues<Key>().Distinct())
        {
            if (!KeyChord.IsChordKey(key)) continue;
            var chord = new KeyChord(key, Ctrl);

            if (KeyChord.Parse(chord.ToString()) != chord) failures.Add($"{key} -> \"{chord}\"");
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(Key.None)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.System)]
    [InlineData(Key.ImeProcessed)]
    [InlineData(Key.DeadCharProcessed)]
    public void IsChordKey_RejectsModifiersAndPseudoKeys(Key key)
    {
        Assert.False(KeyChord.IsChordKey(key));
    }

    // ---------------------------------------------------------------- match ---

    [Fact]
    public void Matches_RequiresTheExactModifierSet()
    {
        var undo = new KeyChord(Key.Z, Ctrl);

        Assert.True(undo.Matches(Key.Z, Ctrl));
        Assert.False(undo.Matches(Key.Z, Ctrl | Shift));   // Ctrl+Shift+Z is not Undo
        Assert.False(undo.Matches(Key.Z, ModifierKeys.None));
        Assert.False(undo.Matches(Key.Y, Ctrl));
    }

    [Fact]
    public void Matches_BareKey_OnlyWithoutModifiers()
    {
        var r = new KeyChord(Key.R, ModifierKeys.None);

        Assert.True(r.Matches(Key.R, ModifierKeys.None));
        Assert.False(r.Matches(Key.R, Shift));
    }
}
