using System.Windows.Media;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The loupe's colour readout: a Shift tap cycles HEX -> RGB -> HSV, and C copies the
/// text exactly as shown.
/// </summary>
public sealed class ColorFormatTests
{
    private static readonly Color Accent = Color.FromRgb(0x3D, 0xA9, 0xFC);

    [Theory]
    [InlineData(ColorFormatKind.Hex, ColorFormatKind.Rgb)]
    [InlineData(ColorFormatKind.Rgb, ColorFormatKind.Hsv)]
    [InlineData(ColorFormatKind.Hsv, ColorFormatKind.Hex)]
    public void Next_CyclesHexRgbHsv(ColorFormatKind current, ColorFormatKind expected)
    {
        Assert.Equal(expected, ColorFormat.Next(current));
    }

    [Fact]
    public void Next_FromAnUnknownValue_StartsOverAtHex()
    {
        Assert.Equal(ColorFormatKind.Hex, ColorFormat.Next((ColorFormatKind)99));
    }

    [Theory]
    [InlineData(ColorFormatKind.Hex, "#3DA9FC")]
    [InlineData(ColorFormatKind.Rgb, "RGB(61, 169, 252)")]
    [InlineData(ColorFormatKind.Hsv, "HSV(206, 76%, 99%)")]
    public void Format_WritesTheColourInTheChosenNotation(ColorFormatKind kind, string expected)
    {
        Assert.Equal(expected, ColorFormat.Format(Accent, kind));
    }

    [Theory]
    [InlineData(255, 0, 0, "HSV(0, 100%, 100%)")]
    [InlineData(0, 255, 0, "HSV(120, 100%, 100%)")]
    [InlineData(0, 0, 255, "HSV(240, 100%, 100%)")]
    [InlineData(255, 255, 0, "HSV(60, 100%, 100%)")]
    [InlineData(0, 0, 0, "HSV(0, 0%, 0%)")]
    [InlineData(255, 255, 255, "HSV(0, 0%, 100%)")]
    [InlineData(128, 128, 128, "HSV(0, 0%, 50%)")]
    public void Format_Hsv_CoversThePrimariesAndTheGreys(byte r, byte g, byte b, string expected)
    {
        Assert.Equal(expected, ColorFormat.Format(Color.FromRgb(r, g, b), ColorFormatKind.Hsv));
    }

    [Fact]
    public void Format_Hsv_RoundsANearlyFullTurnToZeroDegrees()
    {
        // Hue 359.8 rounds to 360, which has to wrap rather than print "HSV(360, ...)".
        var text = ColorFormat.Format(Color.FromRgb(255, 0, 1), ColorFormatKind.Hsv);

        Assert.Equal("HSV(0, 100%, 100%)", text);
    }

    [Fact]
    public void Format_IgnoresAlpha()
    {
        var translucent = Color.FromArgb(0x80, 0xFF, 0x00, 0x00);

        Assert.Equal("#FF0000", ColorFormat.Format(translucent, ColorFormatKind.Hex));
        Assert.Equal("RGB(255, 0, 0)", ColorFormat.Format(translucent, ColorFormatKind.Rgb));
    }

    [Fact]
    public void Format_FallsBackToHex_ForAnUnknownKind()
    {
        Assert.Equal("#3DA9FC", ColorFormat.Format(Accent, (ColorFormatKind)99));
    }
}
