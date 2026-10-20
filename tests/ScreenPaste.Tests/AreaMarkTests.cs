using System.Windows;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A right-click with a fill tool marks the UI element under the cursor: the deepest detected
/// element, in screen px, turned into region-local px and cut to the region.
/// </summary>
public sealed class AreaMarkTests
{
    private const double Min = 4;
    private static readonly Rect Region = new(100, 100, 400, 300);   // on screen

    // ---------------------------------------------------------------- Clip ---

    [Fact]
    public void Clip_AnElementInsideTheRegion_BecomesRegionLocal()
    {
        Assert.Equal(new Rect(50, 20, 50, 40), AreaMark.Clip(new Rect(150, 120, 50, 40), Region, Min));
    }

    [Fact]
    public void Clip_AnElementRunningPastTheRegion_IsCutAtItsEdge()
    {
        Assert.Equal(new Rect(350, 250, 50, 50), AreaMark.Clip(new Rect(450, 350, 100, 100), Region, Min));
    }

    [Fact]
    public void Clip_AnElementCoveringTheRegion_IsTheWholeRegion()
    {
        Assert.Equal(new Rect(0, 0, 400, 300), AreaMark.Clip(new Rect(0, 0, 1920, 1080), Region, Min));
    }

    [Fact]
    public void Clip_AnElementOutsideTheRegion_IsNothing()
    {
        Assert.Null(AreaMark.Clip(new Rect(600, 450, 50, 50), Region, Min));
    }

    [Fact]
    public void Clip_AnElementThatOnlyTouchesTheRegion_IsNothing()
    {
        Assert.Null(AreaMark.Clip(new Rect(500, 150, 60, 60), Region, Min));
    }

    [Fact]
    public void Clip_ASliverAtLeastTheMinimum_IsKept()
    {
        // 5 px of it lie inside the region.
        Assert.Equal(new Rect(395, 50, 5, 60), AreaMark.Clip(new Rect(495, 150, 30, 60), Region, Min));
    }

    [Theory]
    [InlineData(498, 150, 30, 60)]   // 2 px of it inside
    [InlineData(200, 200, 3, 80)]    // thinner than the minimum to begin with
    public void Clip_ASliverThinnerThanTheMinimum_IsNothing(double x, double y, double w, double h)
    {
        Assert.Null(AreaMark.Clip(new Rect(x, y, w, h), Region, Min));
    }

    // ---------------------------------------------------------------- ElementInRegion ---

    private static readonly Rect Button = new(110, 110, 40, 20);
    private static readonly Rect Panel = new(100, 100, 300, 200);
    private static readonly Rect Window = new(50, 50, 800, 600);

    [Fact]
    public void ElementInRegion_MarksTheDeepestElement()
    {
        var chain = new[] { Button, Panel, Window };

        Assert.Equal(new Rect(10, 10, 40, 20), AreaMark.ElementInRegion(chain, Region, Min));
    }

    [Fact]
    public void ElementInRegion_WithOnlyTheWindowDetected_MarksTheWindowsPartOfTheRegion()
    {
        Assert.Equal(new Rect(0, 0, 400, 300), AreaMark.ElementInRegion(new[] { Window }, Region, Min));
    }

    [Fact]
    public void ElementInRegion_WithNothingDetected_IsNothing()
    {
        Assert.Null(AreaMark.ElementInRegion(Array.Empty<Rect>(), Region, Min));
    }
}
