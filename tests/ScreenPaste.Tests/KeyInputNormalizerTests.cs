using System.Windows.Input;
using ScreenPaste.Input;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// WPF reports Alt chords as Key.System and keys an IME saw as Key.ImeProcessed; the
/// normalizer recovers the key the user actually pressed.
/// </summary>
public sealed class KeyInputNormalizerTests
{
    [Theory]
    [InlineData(Key.C, Key.None, Key.None, Key.C)]
    [InlineData(Key.Escape, Key.None, Key.None, Key.Escape)]
    [InlineData(Key.System, Key.F10, Key.None, Key.F10)]
    [InlineData(Key.System, Key.LeftAlt, Key.None, Key.LeftAlt)]
    [InlineData(Key.System, Key.C, Key.None, Key.C)]            // Alt+C
    [InlineData(Key.ImeProcessed, Key.None, Key.C, Key.C)]      // C with a CJK IME open
    [InlineData(Key.ImeProcessed, Key.None, Key.Enter, Key.Enter)]
    public void Effective_UnwrapsSystemAndImeProcessedKeys(Key key, Key systemKey, Key imeKey, Key expected)
    {
        var effective = KeyInputNormalizer.Effective(key, systemKey, imeKey);

        Assert.Equal(expected, effective);
    }

    [Fact]
    public void Effective_UnwrapsDeadCharProcessedKeys()
    {
        var effective = KeyInputNormalizer.Effective(Key.DeadCharProcessed, Key.None, Key.None, Key.OemQuotes);

        Assert.Equal(Key.OemQuotes, effective);
    }

    [Fact]
    public void Effective_IgnoresTheOtherFields_ForAnOrdinaryKey()
    {
        var effective = KeyInputNormalizer.Effective(Key.R, Key.F10, Key.C, Key.OemQuotes);

        Assert.Equal(Key.R, effective);
    }
}
