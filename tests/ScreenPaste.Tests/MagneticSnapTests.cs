using System.Windows;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Shift+drag on a pin (Snipaste's magnetic attach): an edge that comes within the threshold
/// of another pin's or window's edge jumps onto it, either lining up with it or butting
/// against it. Each axis snaps on its own, so a corner snaps when both do; the nearest edge
/// wins; a candidate wins only where the two overlap along the other axis. All physical px.
/// </summary>
public sealed class MagneticSnapTests
{
    private const double Threshold = 10;
    private static readonly Rect Other = new(0, 0, 100, 100);

    [Fact]
    public void Snap_WithoutCandidates_LeavesTheRect()
    {
        var rect = new Rect(105, 20, 50, 50);

        var result = MagneticSnap.Snap(rect, Array.Empty<Rect>(), Threshold);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void Snap_WithNothingWithinTheThreshold_LeavesTheRect()
    {
        var rect = new Rect(120, 20, 50, 50);

        var result = MagneticSnap.Snap(rect, new[] { Other }, Threshold);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void Snap_AttachesTheLeftEdgeToTheRightEdgeOfAnother()
    {
        var result = MagneticSnap.Snap(new Rect(105, 20, 50, 50), new[] { Other }, Threshold);

        Assert.Equal(new Rect(100, 20, 50, 50), result);
    }

    [Fact]
    public void Snap_AttachesTheRightEdgeToTheLeftEdgeOfAnother()
    {
        var result = MagneticSnap.Snap(new Rect(-58, 20, 50, 50), new[] { Other }, Threshold);

        Assert.Equal(new Rect(-50, 20, 50, 50), result);
    }

    [Fact]
    public void Snap_LinesUpEdgesOnTheSameSide()
    {
        // Inside the other one, 4 px from its left edge.
        var result = MagneticSnap.Snap(new Rect(4, 30, 50, 50), new[] { Other }, Threshold);

        Assert.Equal(new Rect(0, 30, 50, 50), result);
    }

    [Fact]
    public void Snap_SnapsACorner_WhenBothAxesAreClose()
    {
        // Just below the other one and nearly lined up with its left edge.
        var result = MagneticSnap.Snap(new Rect(6, 104, 50, 50), new[] { Other }, Threshold);

        Assert.Equal(new Rect(0, 100, 50, 50), result);
    }

    [Fact]
    public void Snap_AtExactlyTheThreshold_StillSnaps()
    {
        var result = MagneticSnap.Snap(new Rect(110, 20, 50, 50), new[] { Other }, Threshold);

        Assert.Equal(100, result.X);
    }

    [Fact]
    public void Snap_TheNearestEdgeWins()
    {
        var nearer = new Rect(0, 0, 103, 100);

        var result = MagneticSnap.Snap(new Rect(105, 20, 50, 50), new[] { Other, nearer }, Threshold);

        Assert.Equal(103, result.X);
    }

    [Fact]
    public void Snap_TakesEachAxisFromItsOwnCandidate()
    {
        // The tall one on the left catches the x; the wide one above catches the y.
        var left = new Rect(0, 0, 100, 300);
        var above = new Rect(120, 0, 300, 100);

        var result = MagneticSnap.Snap(new Rect(104, 106, 50, 50), new[] { left, above }, Threshold);

        Assert.Equal(new Rect(100, 100, 50, 50), result);
    }

    [Fact]
    public void Snap_IgnoresAnEdgeFarAwayAlongTheOtherAxis()
    {
        // Level with the other's right edge, but far below it.
        var rect = new Rect(105, 500, 50, 50);

        var result = MagneticSnap.Snap(rect, new[] { Other }, Threshold);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void Snap_KeepsTheSize()
    {
        var result = MagneticSnap.Snap(new Rect(6, 104, 50, 40), new[] { Other }, Threshold);

        Assert.Equal(50, result.Width);
        Assert.Equal(40, result.Height);
    }

    [Fact]
    public void Snap_SkipsEmptyCandidates()
    {
        var rect = new Rect(105, 20, 50, 50);

        var result = MagneticSnap.Snap(rect, new[] { Rect.Empty }, Threshold);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void Snap_WithAZeroThreshold_LeavesTheRect()
    {
        var rect = new Rect(105, 20, 50, 50);

        var result = MagneticSnap.Snap(rect, new[] { Other }, 0);

        Assert.Equal(rect, result);
    }
}
