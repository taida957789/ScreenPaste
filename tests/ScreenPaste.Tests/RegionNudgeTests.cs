using System.Windows;
using System.Windows.Input;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The arrow keys once a region is chosen: plain arrows move it a pixel, Ctrl+arrows grow
/// the side the arrow points to, Shift+arrows pull the opposite side in. Always kept on the
/// screenshot and never smaller than the minimum region size.
/// </summary>
public sealed class RegionNudgeTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;

    private static readonly Rect Bounds = new(0, 0, 1000, 800);
    private static readonly Rect Start = new(100, 100, 200, 150);
    private const double Min = 20;

    // ---------------------------------------------------------------- move ---

    [Theory]
    [InlineData(Key.Up, 100, 99)]
    [InlineData(Key.Down, 100, 101)]
    [InlineData(Key.Left, 99, 100)]
    [InlineData(Key.Right, 101, 100)]
    public void PlainArrows_MoveTheRegionOnePixel_KeepingItsSize(Key key, double x, double y)
    {
        var r = RegionNudge.Apply(Start, key, None, Bounds, Min);

        Assert.Equal(new Rect(x, y, Start.Width, Start.Height), r);
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void PlainArrows_StopAtTheScreenEdge(Key key)
    {
        // The region covers the whole screenshot: there is nowhere to move it.
        var r = RegionNudge.Apply(Bounds, key, None, Bounds, Min);

        Assert.Equal(Bounds, r);
    }

    [Fact]
    public void PlainArrows_KeepARegionAtTheTopLeftCornerInPlace()
    {
        var corner = new Rect(0, 0, 200, 150);

        Assert.Equal(corner, RegionNudge.Apply(corner, Key.Left, None, Bounds, Min));
        Assert.Equal(corner, RegionNudge.Apply(corner, Key.Up, None, Bounds, Min));
    }

    // ---------------------------------------------------------------- grow ---

    [Theory]
    [InlineData(Key.Up, 100, 99, 200, 151)]      // the N edge moves up
    [InlineData(Key.Down, 100, 100, 200, 151)]   // the S edge moves down
    [InlineData(Key.Left, 99, 100, 201, 150)]    // the W edge moves left
    [InlineData(Key.Right, 100, 100, 201, 150)]  // the E edge moves right
    public void CtrlArrows_GrowTheSideTheArrowPointsTo(Key key, double x, double y, double w, double h)
    {
        var r = RegionNudge.Apply(Start, key, Ctrl, Bounds, Min);

        Assert.Equal(new Rect(x, y, w, h), r);
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void CtrlArrows_CannotGrowPastTheScreenEdge(Key key)
    {
        var r = RegionNudge.Apply(Bounds, key, Ctrl, Bounds, Min);

        Assert.Equal(Bounds, r);
    }

    // ---------------------------------------------------------------- shrink ---

    [Theory]
    [InlineData(Key.Up, 100, 100, 200, 149)]     // the S edge moves up
    [InlineData(Key.Down, 100, 101, 200, 149)]   // the N edge moves down
    [InlineData(Key.Left, 100, 100, 199, 150)]   // the E edge moves left
    [InlineData(Key.Right, 101, 100, 199, 150)]  // the W edge moves right
    public void ShiftArrows_MoveTheOppositeEdgeInTheArrowsDirection(Key key, double x, double y, double w, double h)
    {
        var r = RegionNudge.Apply(Start, key, Shift, Bounds, Min);

        Assert.Equal(new Rect(x, y, w, h), r);
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void ShiftArrows_NeverShrinkBelowTheMinimumSize(Key key)
    {
        var tiny = new Rect(100, 100, Min, Min);

        var r = RegionNudge.Apply(tiny, key, Shift, Bounds, Min);

        Assert.Equal(tiny, r);
    }

    // ---------------------------------------------------------------- inverted limits ---

    [Theory]
    [InlineData(Key.Up, None)]
    [InlineData(Key.Left, Ctrl)]
    [InlineData(Key.Right, Shift)]
    [InlineData(Key.Down, Ctrl)]
    public void Apply_DoesNotThrow_WhenTheRegionLiesOutsideTheBounds(Key key, ModifierKeys modifiers)
    {
        // Arrange: limits invert for a rect stranded beyond the right/bottom edges.
        var stranded = new Rect(1200, 900, 60, 60);

        // Act
        var r = RegionNudge.Apply(stranded, key, modifiers, Bounds, Min);

        // Assert: it degrades to a valid rect instead of throwing.
        Assert.True(r.Width >= Min);
        Assert.True(r.Height >= Min);
    }

    // ---------------------------------------------------------------- other keys ---

    [Theory]
    [InlineData(Key.W, None)]
    [InlineData(Key.Enter, Ctrl)]
    [InlineData(Key.Up, ModifierKeys.Alt)]
    [InlineData(Key.Left, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Right, ModifierKeys.Windows)]
    public void OtherKeysAndModifiers_LeaveTheRegionAlone(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(Start, RegionNudge.Apply(Start, key, modifiers, Bounds, Min));
    }
}
