using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A key an in-app action listens to, as settings.json writes it: a chord ("Ctrl+T", "R",
/// "Shift+R", "1"), a typed character ("[", ",", "!", "Ctrl++"), or a modifier tapped on its
/// own ("Shift", "Alt"). Characters are for the keys whose place depends on the keyboard layout.
/// </summary>
public sealed class ShortcutKeyTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    // ---- parsing ----

    [Theory]
    [InlineData("Ctrl+T", Key.T, Ctrl)]
    [InlineData("R", Key.R, None)]
    [InlineData("Shift+R", Key.R, Shift)]
    [InlineData("1", Key.D1, None)]
    [InlineData("Ctrl+Shift+Tab", Key.Tab, Ctrl | Shift)]
    [InlineData("Esc", Key.Escape, None)]
    [InlineData("Up", Key.Up, None)]
    [InlineData("Apps", Key.Apps, None)]
    public void Parse_ReadsAChord(string text, Key key, ModifierKeys modifiers)
    {
        var binding = ShortcutKey.Parse(text);

        Assert.Equal(ShortcutKey.Chord(key, modifiers), binding);
        Assert.Equal(ShortcutKeyKind.Chord, binding!.Value.Kind);
    }

    [Theory]
    [InlineData("[", '[')]
    [InlineData("]", ']')]
    [InlineData(",", ',')]
    [InlineData(".", '.')]
    [InlineData("`", '`')]
    [InlineData("!", '!')]
    [InlineData("+", '+')]
    [InlineData("-", '-')]
    [InlineData("=", '=')]
    public void Parse_ReadsALoneSymbol_AsATypedCharacter(string text, char character)
    {
        Assert.Equal(ShortcutKey.Char(character), ShortcutKey.Parse(text));
    }

    [Theory]
    [InlineData("Ctrl++", '+', Ctrl)]
    [InlineData("Ctrl+=", '=', Ctrl)]
    [InlineData("Ctrl+-", '-', Ctrl)]
    [InlineData("Ctrl+Alt+[", '[', Ctrl | Alt)]
    public void Parse_ReadsACharacterWithCommandModifiers(string text, char character, ModifierKeys modifiers)
    {
        Assert.Equal(ShortcutKey.Char(character, modifiers), ShortcutKey.Parse(text));
    }

    [Theory]
    [InlineData("Shift", ModifierKeys.Shift)]
    [InlineData("alt", ModifierKeys.Alt)]
    [InlineData("Ctrl", ModifierKeys.Control)]
    [InlineData("Win", ModifierKeys.Windows)]
    public void Parse_ReadsALoneModifier_AsATap(string text, ModifierKeys modifier)
    {
        Assert.Equal(ShortcutKey.Tap(modifier), ShortcutKey.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Shift+[")]      // Shift is part of typing a character, not a modifier of it
    [InlineData("Nope")]
    [InlineData("Ctrl+Nope")]
    [InlineData(" ")]
    public void Parse_RejectsWhatNoKeyCanBe(string? text)
    {
        Assert.Null(ShortcutKey.Parse(text));
    }

    [Theory]
    [InlineData("Ctrl+T")]
    [InlineData("R")]
    [InlineData("Shift+R")]
    [InlineData("1")]
    [InlineData("[")]
    [InlineData("!")]
    [InlineData("Ctrl++")]
    [InlineData("Ctrl+Alt+[")]
    [InlineData("Shift")]
    [InlineData("Alt")]
    [InlineData("Esc")]
    [InlineData("Ctrl+Insert")]
    public void ToString_RoundTripsThroughParse(string text)
    {
        var binding = ShortcutKey.Parse(text)!.Value;

        Assert.Equal(text, binding.ToString());
        Assert.Equal(binding, ShortcutKey.Parse(binding.ToString()));
    }

    [Fact]
    public void Parse_WritesModifiersInTheSettingsOrder()
    {
        Assert.Equal("Ctrl+Shift+Z", ShortcutKey.Parse("shift+ctrl+z")!.Value.ToString());
    }

    // ---- matching a key press ----

    [Fact]
    public void AChord_MatchesItsKeyWithExactlyItsModifiers()
    {
        var undo = ShortcutKey.Chord(Key.Z, Ctrl);

        Assert.True(undo.Matches(KeyInput.Press(Key.Z, Ctrl)));
        Assert.False(undo.Matches(KeyInput.Press(Key.Z, Ctrl | Shift)));
        Assert.False(undo.Matches(KeyInput.Press(Key.Z, None)));
        Assert.False(undo.Matches(KeyInput.Press(Key.Y, Ctrl)));
    }

    [Fact]
    public void ATopRowDigit_AlsoMatchesTheNumberPadDigit()
    {
        var one = ShortcutKey.Chord(Key.D1, None);

        Assert.True(one.Matches(KeyInput.Press(Key.D1, None)));
        Assert.True(one.Matches(KeyInput.Press(Key.NumPad1, None)));
        Assert.False(one.Matches(KeyInput.Press(Key.NumPad2, None)));
        Assert.True(ShortcutKey.Chord(Key.D6, Ctrl).Matches(KeyInput.Press(Key.NumPad6, Ctrl)));
    }

    [Fact]
    public void ANumberPadDigit_MatchesOnlyItself()
    {
        Assert.False(ShortcutKey.Chord(Key.NumPad1, None).Matches(KeyInput.Press(Key.D1, None)));
        Assert.True(ShortcutKey.Chord(Key.NumPad1, None).Matches(KeyInput.Press(Key.NumPad1, None)));
    }

    [Fact]
    public void ACharacter_MatchesAKeyThatTypesIt_ShiftOrNot()
    {
        var equals = ShortcutKey.Char('=');

        Assert.True(equals.Matches(KeyInput.Press(Key.OemPlus, None, layoutText: "=")));
        Assert.True(equals.Matches(KeyInput.Press(Key.OemPlus, Shift, layoutText: "=")));
        Assert.False(equals.Matches(KeyInput.Press(Key.OemPlus, Ctrl, layoutText: "=")));
        Assert.False(equals.Matches(KeyInput.Press(Key.OemMinus, None, layoutText: "-")));
        Assert.False(equals.Matches(KeyInput.Press(Key.OemPlus, None, layoutText: null)));
    }

    [Fact]
    public void ACharacterWithCtrl_MatchesItsKeyWithCtrl_ShiftOrNot()
    {
        var opacityUp = ShortcutKey.Char('=', Ctrl);

        Assert.True(opacityUp.Matches(KeyInput.Press(Key.OemPlus, Ctrl, layoutText: "=")));
        Assert.True(opacityUp.Matches(KeyInput.Press(Key.OemPlus, Ctrl | Shift, layoutText: "=")));
        Assert.False(opacityUp.Matches(KeyInput.Press(Key.OemPlus, None, layoutText: "=")));
    }

    [Theory]
    [InlineData(Key.Add, '+')]
    [InlineData(Key.Subtract, '-')]
    [InlineData(Key.Multiply, '*')]
    [InlineData(Key.Divide, '/')]
    public void ACharacter_MatchesTheNumberPadKeyThatTypesIt(Key key, char character)
    {
        Assert.True(ShortcutKey.Char(character).Matches(KeyInput.Press(key, None)));
        Assert.True(ShortcutKey.Char(character, Ctrl).Matches(KeyInput.Press(key, Ctrl)));
    }

    [Fact]
    public void ACharacter_MatchesTheSameCharacterTyped_WhateverTypedIt()
    {
        var bang = ShortcutKey.Char('!');

        Assert.True(bang.Matches(KeyInput.Typed("!")));
        Assert.False(bang.Matches(KeyInput.Typed("1")));
        Assert.False(bang.Matches(KeyInput.Press(Key.D1, Shift, layoutText: "1")));
    }

    [Fact]
    public void ACharacterWithCtrl_IsNeverTyped()
    {
        Assert.False(ShortcutKey.Char('+', Ctrl).Matches(KeyInput.Typed("+")));
    }

    [Fact]
    public void AChord_IsNeverTyped_NorTapped()
    {
        var r = ShortcutKey.Chord(Key.R, None);

        Assert.False(r.Matches(KeyInput.Typed("r")));
        Assert.False(r.Matches(KeyInput.Tapped(ModifierKeys.Shift)));
    }

    [Fact]
    public void ATap_MatchesOnlyThatModifierTapped()
    {
        var shiftTap = ShortcutKey.Tap(ModifierKeys.Shift);

        Assert.True(shiftTap.Matches(KeyInput.Tapped(ModifierKeys.Shift)));
        Assert.False(shiftTap.Matches(KeyInput.Tapped(ModifierKeys.Alt)));
        Assert.False(shiftTap.Matches(KeyInput.Press(Key.LeftShift, Shift)));
        Assert.False(shiftTap.Matches(KeyInput.Typed("A")));
    }

    // ---- telling which bindings can clash ----

    [Fact]
    public void Collides_IsTrue_ForTheSameKeyOrDigitTwin()
    {
        Assert.True(ShortcutKey.Chord(Key.D1, None).Collides(ShortcutKey.Chord(Key.D1, None)));
        Assert.True(ShortcutKey.Chord(Key.D1, None).Collides(ShortcutKey.Chord(Key.NumPad1, None)));
        Assert.True(ShortcutKey.Chord(Key.NumPad1, None).Collides(ShortcutKey.Chord(Key.D1, None)));
        Assert.False(ShortcutKey.Chord(Key.D1, None).Collides(ShortcutKey.Chord(Key.D1, Ctrl)));
        Assert.False(ShortcutKey.Char('[').Collides(ShortcutKey.Chord(Key.OemOpenBrackets, None)));
        Assert.True(ShortcutKey.Tap(ModifierKeys.Shift).Collides(ShortcutKey.Tap(ModifierKeys.Shift)));
    }

    // A US layout: "!" is Shift+1, "{" is Shift+[, "+" is Shift+=; "[" and "-" take no Shift.
    private static string? UsShiftedKeyOf(char c) => c switch
    {
        '!' => "1",
        '{' => "[",
        '+' => "=",
        _ => null,
    };

    // The same layout's punctuation keys, as they type unshifted.
    private static string? UsKeyText(Key key) => key switch
    {
        Key.OemOpenBrackets => "[",
        Key.OemPlus => "=",
        _ => null,
    };

    [Theory]
    [InlineData("!", "Shift+1")]
    [InlineData("{", "Shift+[")]
    [InlineData("+", "Shift+=")]
    [InlineData("[", "[")]               // typed with no modifier: the key itself
    [InlineData("-", "-")]
    [InlineData("Ctrl++", "Ctrl++")]     // with Ctrl it is the key that types "+" unshifted (the number pad's)
    [InlineData("Ctrl+=", "Ctrl+=")]
    [InlineData("Ctrl+T", "Ctrl+T")]
    [InlineData("Shift+R", "Shift+R")]
    [InlineData("Shift+OemOpenBrackets", "Shift+[")]   // a chord on a punctuation key: its character
    [InlineData("Ctrl+OemPlus", "Ctrl+=")]
    [InlineData("Shift", "Shift")]       // a tap
    public void ToDisplayString_SpellsAShiftedSymbolAsItsShiftChord(string stored, string shown)
    {
        var key = ShortcutKey.Parse(stored)!.Value;
        Assert.Equal(shown, key.ToDisplayString(UsShiftedKeyOf, UsKeyText));
        Assert.Equal(stored, key.ToString());   // settings.json keeps the character or the key's name
    }

    [Fact]
    public void ShiftedKeyOf_IsNull_ForACharacterNoKeyTypes()
    {
        Assert.Null(KeyboardLayout.ShiftedKeyOf('☃'));
    }

    [Fact]
    public void HasCommandModifier_TellsAGestureSafeToRunWhileTyping()
    {
        Assert.True(ShortcutKey.Chord(Key.S, Ctrl).HasCommandModifier);
        Assert.True(ShortcutKey.Char('+', Ctrl).HasCommandModifier);
        Assert.False(ShortcutKey.Chord(Key.R, Shift).HasCommandModifier);
        Assert.False(ShortcutKey.Char('[').HasCommandModifier);
        Assert.False(ShortcutKey.Tap(ModifierKeys.Alt).HasCommandModifier);
    }
}
