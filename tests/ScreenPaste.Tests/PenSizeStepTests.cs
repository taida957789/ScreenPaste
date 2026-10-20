using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The wheel, 1/2 and [/] step the current tool's width (font size for text, strength for
/// blur, border for the magnifier) within its toolbar slider's range, and the wheel on a
/// colour swatch steps the colour's opacity by 5%.
/// </summary>
public sealed class PenSizeStepTests
{
    private const int Up = 1, Down = -1;
    private const int Precision = 9;

    [Theory]
    [InlineData(ToolKind.MarkerPen, 4, Up, 5)]
    [InlineData(ToolKind.Highlighter, 18, Down, 17)]
    [InlineData(ToolKind.Shape, 3, Up, 4)]
    [InlineData(ToolKind.Line, 3, Down, 2)]
    [InlineData(ToolKind.Text, 24, Up, 26)]
    [InlineData(ToolKind.Text, 24, Down, 22)]
    [InlineData(ToolKind.Blur, 12, Up, 14)]
    [InlineData(ToolKind.Magnify, 3, Up, 4)]
    public void Next_StepsByTheToolsStep(ToolKind tool, double value, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.Next(tool, value, direction), Precision);
    }

    [Theory]
    [InlineData(ToolKind.MarkerPen, 40, Up, 40)]
    [InlineData(ToolKind.MarkerPen, 1, Down, 1)]
    [InlineData(ToolKind.Shape, 20, Up, 20)]
    [InlineData(ToolKind.Line, 1, Down, 1)]
    [InlineData(ToolKind.Text, 96, Up, 96)]
    [InlineData(ToolKind.Text, 10, Down, 10)]
    [InlineData(ToolKind.Blur, 40, Up, 40)]
    [InlineData(ToolKind.Blur, 2, Down, 2)]
    [InlineData(ToolKind.Magnify, 0, Down, 0)]
    [InlineData(ToolKind.Magnify, 12, Up, 12)]
    public void Next_StopsAtTheEndsOfTheRange(ToolKind tool, double value, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.Next(tool, value, direction), Precision);
    }

    [Theory]
    [InlineData(4.4, Up, 5)]
    [InlineData(4.4, Down, 4)]
    [InlineData(4.6, Up, 5)]
    [InlineData(4.6, Down, 4)]
    public void Next_FromADraggedSliderValue_LandsOnTheStepGrid(double value, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.Next(ToolKind.MarkerPen, value, direction), Precision);
    }

    [Theory]
    [InlineData(23.7, Up, 24)]
    [InlineData(23.7, Down, 22)]
    public void Next_ForText_LandsOnEvenSizes(double value, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.Next(ToolKind.Text, value, direction), Precision);
    }

    [Theory]
    [InlineData(55, Down, 40)]
    [InlineData(0.5, Up, 1)]
    public void Next_PullsAValueFromOutsideTheRangeBackIn(double value, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.Next(ToolKind.MarkerPen, value, direction), Precision);
    }

    [Theory]
    [InlineData(ToolKind.None)]
    [InlineData(ToolKind.Sticker)]
    public void Next_ForAToolWithoutAWidth_ChangesNothing(ToolKind tool)
    {
        Assert.Equal(7.3, PenSizeStep.Next(tool, 7.3, Up), Precision);
        Assert.Null(PenSizeStep.RangeOf(tool));
    }

    [Fact]
    public void Next_WithNoDirection_ChangesNothing()
    {
        Assert.Equal(7.3, PenSizeStep.Next(ToolKind.MarkerPen, 7.3, 0), Precision);
    }

    [Fact]
    public void RangeOf_MatchesTheToolbarSliders()
    {
        Assert.Equal(new PenSizeRange(1, 40, 1), PenSizeStep.RangeOf(ToolKind.MarkerPen));
        Assert.Equal(new PenSizeRange(1, 40, 1), PenSizeStep.RangeOf(ToolKind.Highlighter));
        Assert.Equal(new PenSizeRange(1, 20, 1), PenSizeStep.RangeOf(ToolKind.Shape));
        Assert.Equal(new PenSizeRange(1, 20, 1), PenSizeStep.RangeOf(ToolKind.Line));
        Assert.Equal(new PenSizeRange(10, 96, 2), PenSizeStep.RangeOf(ToolKind.Text));
        Assert.Equal(new PenSizeRange(2, 40, 2), PenSizeStep.RangeOf(ToolKind.Blur));
        Assert.Equal(new PenSizeRange(0, 12, 1), PenSizeStep.RangeOf(ToolKind.Magnify));
    }

    // ---------------------------------------------------------------- opacity ---

    [Theory]
    [InlineData(0.45, Up, 0.5)]
    [InlineData(0.45, Down, 0.4)]
    [InlineData(1.0, Up, 1.0)]
    [InlineData(0.1, Down, 0.1)]
    [InlineData(0.42, Up, 0.45)]
    public void NextOpacity_StepsByFivePercent_WithinTheSlidersRange(double opacity, int direction, double expected)
    {
        Assert.Equal(expected, PenSizeStep.NextOpacity(opacity, direction), Precision);
    }

    [Fact]
    public void NextOpacity_FromAByteAlpha_StillMovesDown()
    {
        // A colour's alpha is a byte: 45% is stored as 115/255 = 0.45098..., which must not
        // round back up to the same byte on the way down.
        double fromByte = Math.Round(0.45 * 255) / 255;

        double down = PenSizeStep.NextOpacity(fromByte, Down);

        Assert.Equal(0.4, down, Precision);
        Assert.NotEqual(Math.Round(fromByte * 255), Math.Round(down * 255));
    }
}
