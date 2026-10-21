using System.Windows.Input;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Snipaste's tool keys in the capture overlay: T/B/M/E pick a tool, Ctrl+0..9 pick one by
/// Snipaste's meaning (the toolbar order is ScreenPaste's own), Ctrl+Tab cycles, Tab flips the
/// current tool's variant, 1/2 or [/] step its width, and + / - the number marker's number.
/// </summary>
public sealed class ToolShortcutsTests
{
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
                ToolKind.Line, ToolKind.Blur, ToolKind.Magnify, ToolKind.Number, ToolKind.Eraser,
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
    [InlineData(ToolKind.Number, false)]
    [InlineData(ToolKind.Eraser, false)]
    public void HasVariant_OnlyForToolsTabCanFlip(ToolKind tool, bool expected)
    {
        Assert.Equal(expected, ToolShortcuts.HasVariant(tool));
    }
}
