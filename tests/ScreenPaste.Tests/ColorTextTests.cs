using System.Windows.Media;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A colour value on the clipboard (Snipaste pastes it as a colour card): #RGB, #RRGGBB,
/// #AARRGGBB, RGB(r, g, b), a bare r,g,b, and the loupe's own HSV(h, s%, v%), in any case
/// and with any spacing. Anything else, out-of-range values or ordinary text included, is
/// not a colour and pastes as text.
/// </summary>
public sealed class ColorTextTests
{
    private static Color Parsed(string text)
    {
        Assert.True(ColorText.TryParse(text, out var color), $"'{text}' should parse");
        return color;
    }

    // ---------------------------------------------------------------- hex ---

    [Theory]
    [InlineData("#3DA9FC", 0xFF, 0x3D, 0xA9, 0xFC)]
    [InlineData("#3da9fc", 0xFF, 0x3D, 0xA9, 0xFC)]
    [InlineData("#F00", 0xFF, 0xFF, 0x00, 0x00)]
    [InlineData("#abc", 0xFF, 0xAA, 0xBB, 0xCC)]
    [InlineData("#803DA9FC", 0x80, 0x3D, 0xA9, 0xFC)]
    [InlineData("  #3DA9FC \r\n", 0xFF, 0x3D, 0xA9, 0xFC)]
    public void Hex_ReadsEveryLength(string text, byte a, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromArgb(a, r, g, b), Parsed(text));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#1234")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("#GGHHII")]
    [InlineData("# 3DA9FC")]
    [InlineData("3DA9FC")]       // without the # it could be a word: "decade", "facade"
    [InlineData("#3DA9FC is blue")]
    public void Hex_RejectsOtherLengthsAndDigits(string text)
    {
        Assert.False(ColorText.TryParse(text, out _));
    }

    // ---------------------------------------------------------------- rgb ---

    [Theory]
    [InlineData("RGB(61, 169, 252)")]
    [InlineData("rgb(61,169,252)")]
    [InlineData("Rgb( 61 , 169 , 252 )")]
    [InlineData("61, 169, 252")]
    [InlineData("61,169,252")]
    [InlineData("  61 ,169,  252  ")]
    public void Rgb_ReadsTheFunctionAndTheBareTriple(string text)
    {
        Assert.Equal(Color.FromRgb(61, 169, 252), Parsed(text));
    }

    [Fact]
    public void Rgb_ReadsTheExtremes()
    {
        Assert.Equal(Color.FromRgb(0, 0, 0), Parsed("0,0,0"));
        Assert.Equal(Color.FromRgb(255, 255, 255), Parsed("RGB(255, 255, 255)"));
    }

    [Theory]
    [InlineData("256, 0, 0")]
    [InlineData("RGB(0, -1, 0)")]
    [InlineData("1, 2")]
    [InlineData("1, 2, 3, 4")]
    [InlineData("1.5, 2, 3")]
    [InlineData("1,,2,3")]
    [InlineData("RGB(1, 2, 3")]
    [InlineData("RGB 1, 2, 3")]
    [InlineData("a, b, c")]
    public void Rgb_RejectsOutOfRangeAndMalformedTriples(string text)
    {
        Assert.False(ColorText.TryParse(text, out _));
    }

    // ---------------------------------------------------------------- hsv ---

    [Fact]
    public void Hsv_ReadsWhatTheLoupeCopies()
    {
        var blue = Color.FromRgb(0x3D, 0xA9, 0xFC);
        var text = ColorFormat.Format(blue, ColorFormatKind.Hsv);   // HSV(206, 76%, 99%)

        var parsed = Parsed(text);

        Assert.InRange(parsed.R, 0x3D - 3, 0x3D + 3);
        Assert.InRange(parsed.G, 0xA9 - 3, 0xA9 + 3);
        Assert.InRange(parsed.B, 0xFC - 3, 0xFC + 3);
    }

    [Theory]
    [InlineData("HSV(0, 100%, 100%)", 255, 0, 0)]
    [InlineData("hsv(120,100,100)", 0, 255, 0)]
    [InlineData("HSV(0, 0%, 0%)", 0, 0, 0)]
    [InlineData("HSV(60, 100%, 100%)", 255, 255, 0)]
    [InlineData("HSV(180, 100%, 100%)", 0, 255, 255)]
    [InlineData("HSV(240, 100%, 100%)", 0, 0, 255)]
    [InlineData("HSV(300, 100%, 100%)", 255, 0, 255)]
    [InlineData("HSV(0, 0%, 100%)", 255, 255, 255)]
    public void Hsv_ReadsPercentOrPlainNumbers(string text, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), Parsed(text));
    }

    [Theory]
    [InlineData("HSV(360, 50%, 50%)")]
    [InlineData("HSV(10, 101%, 50%)")]
    [InlineData("HSV(10, 50%)")]
    public void Hsv_RejectsOutOfRangeValues(string text)
    {
        Assert.False(ColorText.TryParse(text, out _));
    }

    // ---------------------------------------------------------------- not colours ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("red")]
    [InlineData("The sky is #3DA9FC today.")]
    [InlineData("Meet at 10, 20 or 30 minutes past.")]
    [InlineData("RGB(61, 169, 252) and more")]
    [InlineData("#3DA9FC\n#FF0000")]
    public void OrdinaryText_IsNotAColour(string? text)
    {
        Assert.False(ColorText.TryParse(text, out _));
    }
}
