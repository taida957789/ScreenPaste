using System.Windows;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Which pin is under the cursor for the click-through hotkey: the pins' window rects in
/// physical screen px, topmost first. The topmost one containing the point wins; the left and
/// top edges belong to a rect and the right and bottom ones do not, as with GetWindowRect.
/// </summary>
public sealed class PinHitTestTests
{
    private static readonly Rect Front = new(100, 100, 200, 100);   // 100-300 x 100-200
    private static readonly Rect Back = new(150, 50, 400, 300);     // 150-550 x 50-350

    [Fact]
    public void ThePointOverBoth_FindsTheTopmost()
    {
        Assert.Equal(0, PinHitTest.Find(new[] { Front, Back }, new Point(200, 150)));
        Assert.Equal(0, PinHitTest.Find(new[] { Back, Front }, new Point(200, 150)));
    }

    [Fact]
    public void ThePointOverTheBackOneOnly_FindsIt()
    {
        Assert.Equal(1, PinHitTest.Find(new[] { Front, Back }, new Point(400, 300)));
    }

    [Theory]
    [InlineData(100, 100)]   // top-left corner
    [InlineData(299, 199)]   // last pixel inside
    public void TheLeftAndTopEdges_AreInside(double x, double y)
    {
        Assert.Equal(0, PinHitTest.Find(new[] { Front }, new Point(x, y)));
    }

    [Theory]
    [InlineData(300, 150)]   // right edge
    [InlineData(200, 200)]   // bottom edge
    [InlineData(99, 150)]
    [InlineData(200, 99)]
    public void TheRightAndBottomEdges_AreOutside(double x, double y)
    {
        Assert.Null(PinHitTest.Find(new[] { Front }, new Point(x, y)));
    }

    [Fact]
    public void NoPinUnderThePoint_FindsNone()
    {
        Assert.Null(PinHitTest.Find(new[] { Front, Back }, new Point(10, 10)));
        Assert.Null(PinHitTest.Find(Array.Empty<Rect>(), new Point(10, 10)));
    }

    [Fact]
    public void AnEmptyRect_IsNeverHit()
    {
        Assert.Null(PinHitTest.Find(new[] { Rect.Empty, new Rect(10, 10, 0, 0) }, new Point(10, 10)));
    }

    [Fact]
    public void NegativeCoordinates_OnAMonitorLeftOfThePrimary_Work()
    {
        Assert.Equal(0, PinHitTest.Find(new[] { new Rect(-1920, 0, 300, 300) }, new Point(-1800, 10)));
    }
}
