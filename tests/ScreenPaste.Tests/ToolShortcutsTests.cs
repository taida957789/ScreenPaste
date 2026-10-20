using System.Windows.Input;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Snipaste's tool keys in the capture overlay: T/B/M pick a tool, Ctrl+0..9 pick one by
/// Snipaste's meaning (the toolbar order is ScreenPaste's own), Ctrl+Tab cycles, Tab flips the
/// current tool's variant, and 1/2 or [/] step its width.
/// </summary>
public sealed class ToolShortcutsTests
{
    // ---------------------------------------------------------------- letters ---

    [Theory]
    [InlineData(Key.T, ToolKind.Text)]
    [InlineData(Key.B, ToolKind.MarkerPen)]
    [InlineData(Key.M, ToolKind.Blur)]
    public void ToolForLetter_MapsSnipastesLetters(Key key, ToolKind expected)
    {
        Assert.Equal(expected, ToolShortcuts.ToolForLetter(key));
    }

    [Theory]
    [InlineData(Key.J)]
    [InlineData(Key.C)]   // the loupe's colour copy
    [InlineData(Key.V)]   // the size copy
    [InlineData(Key.W)]   // a cursor nudge
    [InlineData(Key.D1)]
    public void ToolForLetter_LeavesOtherKeysUnmapped(Key key)
    {
        Assert.Null(ToolShortcuts.ToolForLetter(key));
    }

    // ---------------------------------------------------------------- Ctrl+digit ---

    [Theory]
    [InlineData(1, ToolKind.Shape)]
    [InlineData(2, ToolKind.Line)]
    [InlineData(3, ToolKind.MarkerPen)]
    [InlineData(4, ToolKind.Highlighter)]
    [InlineData(5, ToolKind.Blur)]
    [InlineData(6, ToolKind.Text)]
    [InlineData(8, ToolKind.Magnify)]
    [InlineData(0, ToolKind.Sticker)]
    public void ToolForIndex_FollowsSnipastesMeaning(int index, ToolKind expected)
    {
        Assert.Equal(expected, ToolShortcuts.ToolForIndex(index, ToolShortcuts.SnipasteLayout));
    }

    [Theory]
    [InlineData(7)]    // Snipaste's number tool: no ScreenPaste tool there yet
    [InlineData(9)]    // Snipaste's eraser: likewise
    [InlineData(-1)]
    [InlineData(10)]
    public void ToolForIndex_LeavesUnmappedIndicesEmpty(int index)
    {
        Assert.Null(ToolShortcuts.ToolForIndex(index, ToolShortcuts.SnipasteLayout));
    }

    [Fact]
    public void ToolForIndex_ReadsTheLayoutItIsGiven()
    {
        var layout = new[] { ToolKind.None, ToolKind.Text };

        Assert.Equal(ToolKind.Text, ToolShortcuts.ToolForIndex(1, layout));
        Assert.Null(ToolShortcuts.ToolForIndex(0, layout));
        Assert.Null(ToolShortcuts.ToolForIndex(2, layout));
    }

    [Theory]
    [InlineData(Key.D0, 0)]
    [InlineData(Key.D5, 5)]
    [InlineData(Key.D9, 9)]
    [InlineData(Key.NumPad0, 0)]
    [InlineData(Key.NumPad7, 7)]
    public void DigitOf_ReadsTheTopRowAndTheNumberPad(Key key, int expected)
    {
        Assert.Equal(expected, ToolShortcuts.DigitOf(key));
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.F1)]
    [InlineData(Key.OemPlus)]
    public void DigitOf_IsNullForOtherKeys(Key key)
    {
        Assert.Null(ToolShortcuts.DigitOf(key));
    }

    // ---------------------------------------------------------------- Ctrl+Tab ---

    private static readonly ToolKind[] Order = { ToolKind.MarkerPen, ToolKind.Text, ToolKind.Blur };

    [Fact]
    public void Cycle_StepsThroughTheOrder()
    {
        Assert.Equal(ToolKind.Text, ToolShortcuts.Cycle(Order, ToolKind.MarkerPen, +1));
        Assert.Equal(ToolKind.MarkerPen, ToolShortcuts.Cycle(Order, ToolKind.Text, -1));
    }

    [Fact]
    public void Cycle_WrapsAroundAtBothEnds()
    {
        Assert.Equal(ToolKind.MarkerPen, ToolShortcuts.Cycle(Order, ToolKind.Blur, +1));
        Assert.Equal(ToolKind.Blur, ToolShortcuts.Cycle(Order, ToolKind.MarkerPen, -1));
    }

    [Fact]
    public void Cycle_FromNoTool_StartsAtTheFirstOrTheLast()
    {
        Assert.Equal(ToolKind.MarkerPen, ToolShortcuts.Cycle(Order, ToolKind.None, +1));
        Assert.Equal(ToolKind.Blur, ToolShortcuts.Cycle(Order, ToolKind.None, -1));
    }

    [Fact]
    public void Cycle_FromAToolOutsideTheOrder_StartsAtTheFirstOrTheLast()
    {
        Assert.Equal(ToolKind.MarkerPen, ToolShortcuts.Cycle(Order, ToolKind.Sticker, +1));
        Assert.Equal(ToolKind.Blur, ToolShortcuts.Cycle(Order, ToolKind.Sticker, -1));
    }

    [Fact]
    public void Cycle_SkipsNone()
    {
        var order = new[] { ToolKind.MarkerPen, ToolKind.None, ToolKind.Text };

        Assert.Equal(ToolKind.Text, ToolShortcuts.Cycle(order, ToolKind.MarkerPen, +1));
        Assert.Equal(ToolKind.MarkerPen, ToolShortcuts.Cycle(order, ToolKind.Text, +1));
    }

    [Fact]
    public void Cycle_OverAnEmptyOrder_KeepsTheCurrentTool()
    {
        Assert.Equal(ToolKind.Text, ToolShortcuts.Cycle(Array.Empty<ToolKind>(), ToolKind.Text, +1));
    }

    [Fact]
    public void CycleOrder_IsTheToolbarOrder_WithoutTheStickerDialog()
    {
        Assert.Equal(
            new[]
            {
                ToolKind.MarkerPen, ToolKind.Highlighter, ToolKind.Text, ToolKind.Shape,
                ToolKind.Line, ToolKind.Blur, ToolKind.Magnify,
            },
            ToolShortcuts.CycleOrder);
    }

    // ---------------------------------------------------------------- Tab ---

    [Theory]
    [InlineData(ShapeKind.Rectangle, ShapeKind.RoundedRectangle)]
    [InlineData(ShapeKind.RoundedRectangle, ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Ellipse, ShapeKind.Rectangle)]
    public void NextShapeKind_CyclesRectangleRoundedEllipse(ShapeKind kind, ShapeKind expected)
    {
        Assert.Equal(expected, ToolShortcuts.NextShapeKind(kind));
    }

    [Theory]
    [InlineData(BlurKind.Gaussian, BlurKind.Mosaic)]
    [InlineData(BlurKind.Mosaic, BlurKind.Gaussian)]
    public void NextBlurKind_TogglesGaussianAndMosaic(BlurKind kind, BlurKind expected)
    {
        Assert.Equal(expected, ToolShortcuts.NextBlurKind(kind));
    }

    [Theory]
    [InlineData(ToolKind.Shape, true)]
    [InlineData(ToolKind.Line, true)]
    [InlineData(ToolKind.Blur, true)]
    [InlineData(ToolKind.Magnify, true)]
    [InlineData(ToolKind.None, false)]
    [InlineData(ToolKind.MarkerPen, false)]
    [InlineData(ToolKind.Text, false)]
    [InlineData(ToolKind.Sticker, false)]
    public void HasVariant_OnlyForToolsTabCanFlip(ToolKind tool, bool expected)
    {
        Assert.Equal(expected, ToolShortcuts.HasVariant(tool));
    }

    // ---------------------------------------------------------------- width keys ---

    [Theory]
    [InlineData(Key.D1, -1)]
    [InlineData(Key.NumPad1, -1)]
    [InlineData(Key.D2, 1)]
    [InlineData(Key.NumPad2, 1)]
    [InlineData(Key.D3, 0)]
    [InlineData(Key.OemOpenBrackets, 0)]   // matched by the character it types instead
    public void PenSizeDirection_OfAKey(Key key, int expected)
    {
        Assert.Equal(expected, ToolShortcuts.PenSizeDirection(key));
    }

    [Theory]
    [InlineData("[", -1)]
    [InlineData("]", 1)]
    [InlineData("1", 0)]
    [InlineData("", 0)]
    [InlineData("[]", 0)]
    public void PenSizeDirection_OfTypedText(string text, int expected)
    {
        Assert.Equal(expected, ToolShortcuts.PenSizeDirection(text));
    }

    // ---------------------------------------------------------------- tooltips ---

    [Theory]
    [InlineData(ToolKind.MarkerPen, "Ctrl+3, B")]
    [InlineData(ToolKind.Text, "Ctrl+6, T")]
    [InlineData(ToolKind.Blur, "Ctrl+5, M")]
    [InlineData(ToolKind.Shape, "Ctrl+1")]
    [InlineData(ToolKind.Sticker, "Ctrl+0")]
    [InlineData(ToolKind.None, "")]
    public void KeysLabel_ListsEveryKeyThatPicksTheTool(ToolKind tool, string expected)
    {
        Assert.Equal(expected, ToolShortcuts.KeysLabel(tool));
    }
}
