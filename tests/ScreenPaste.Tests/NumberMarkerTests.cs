using System.Windows;
using System.Windows.Media;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// How a number marker looks: a filled circle centred on the click, its number sized to fit
/// and drawn in white or black, whichever reads better on the circle's colour.
/// </summary>
public sealed class NumberMarkerTests
{
    private const int Precision = 6;

    [Fact]
    public void Frame_IsTheCircleCentredOnTheClick()
    {
        Assert.Equal(new Rect(86, 36, 28, 28), NumberMarker.Frame(new Point(100, 50), 28));
    }

    [Theory]
    [InlineData(28, 1, 16.8)]
    [InlineData(28, 9, 16.8)]
    [InlineData(28, 12, 14)]
    [InlineData(28, 123, 11.2)]
    [InlineData(40, 7, 24)]
    public void FontSizeFor_ShrinksTheTextAsTheNumberGetsLonger(double diameter, int number, double expected)
    {
        Assert.Equal(expected, NumberMarker.FontSizeFor(diameter, number), Precision);
    }

    [Theory]
    [InlineData(0xFF, 0x3B, 0x30)]   // the default red
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0x3D, 0xA9, 0xFC)]   // the palette's blue
    [InlineData(0x32, 0xCD, 0x32)]   // lime green
    public void TextColorFor_ADarkOrSaturatedFill_IsWhite(byte r, byte g, byte b)
    {
        Assert.Equal(Colors.White, NumberMarker.TextColorFor(Color.FromRgb(r, g, b)));
    }

    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0xFF, 0xEB, 0x3B)]   // the palette's yellow
    public void TextColorFor_ALightFill_IsBlack(byte r, byte g, byte b)
    {
        Assert.Equal(Colors.Black, NumberMarker.TextColorFor(Color.FromRgb(r, g, b)));
    }

    [Fact]
    public void TextColorFor_IgnoresTheFillsOpacity()
    {
        Assert.Equal(Colors.Black, NumberMarker.TextColorFor(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));
    }

    [Fact]
    public void PressGrabs_TheNumberTool_PlacesItsMarkerOverOtherAnnotations()
    {
        Assert.False(NumberMarker.PressGrabs(ToolKind.Number, onNumberMarker: false));
    }

    [Fact]
    public void PressGrabs_TheNumberTool_PicksUpANumberMarker()
    {
        Assert.True(NumberMarker.PressGrabs(ToolKind.Number, onNumberMarker: true));
    }

    [Theory]
    [InlineData(ToolKind.None)]
    [InlineData(ToolKind.MarkerPen)]
    [InlineData(ToolKind.Text)]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.Line)]
    [InlineData(ToolKind.Blur)]
    [InlineData(ToolKind.Magnify)]
    public void PressGrabs_OtherTools_PickUpAnyAnnotation(ToolKind tool)
    {
        Assert.True(NumberMarker.PressGrabs(tool, onNumberMarker: false));
        Assert.True(NumberMarker.PressGrabs(tool, onNumberMarker: true));
    }
}
