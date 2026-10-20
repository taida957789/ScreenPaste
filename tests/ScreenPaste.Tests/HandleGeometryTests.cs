using System.Windows;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The 8-handle resize maths shared by the capture region and a magnifier's framed source.
/// Handle indices: 0=NW 1=N 2=NE 3=W 4=E 5=SW 6=S 7=SE.
/// </summary>
public sealed class HandleGeometryTests
{
    private static readonly Rect Bounds = new(0, 0, 1000, 800);
    private const double Min = 6;

    public static TheoryData<int> AllHandles()
    {
        var data = new TheoryData<int>();
        for (int i = 0; i < HandleGeometry.Count; i++) data.Add(i);
        return data;
    }

    // ---------------------------------------------------------------- regression ---

    /// <summary>
    /// Regression: a magnifier's framed source is stored in region-local coords, so shrinking
    /// the capture region can leave it outside the region. The clamp limits then invert, and
    /// Math.Clamp throws ArgumentException — which the app's dispatcher handler turned into
    /// "close the overlay", discarding every annotation in the session.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllHandles))]
    public void DragEdges_DoesNotThrow_WhenStartLiesOutsideShrunkenBounds(int handle)
    {
        // Arrange: region shrank to 500x50; the framed source is still out at x=900,y=100.
        var shrunk = new Rect(0, 0, 500, 50);
        var stranded = new Rect(900, 100, 60, 60);

        // Act
        var result = HandleGeometry.DragEdges(stranded, handle, 5, 5, Min, shrunk);

        // Assert: it degrades to a minimum-size rect instead of throwing.
        Assert.True(result.Width >= Min);
        Assert.True(result.Height >= Min);
    }

    // ---------------------------------------------------------------- edge ownership ---

    [Theory]
    [InlineData(0, true, false, true, false)]   // NW
    [InlineData(1, false, false, true, false)]  // N
    [InlineData(2, false, true, true, false)]   // NE
    [InlineData(3, true, false, false, false)]  // W
    [InlineData(4, false, true, false, false)]  // E
    [InlineData(5, true, false, false, true)]   // SW
    [InlineData(6, false, false, false, true)]  // S
    [InlineData(7, false, true, false, true)]   // SE
    public void EdgesFor_MapsIndexToCompassEdges(int index, bool w, bool e, bool n, bool s)
    {
        var edges = HandleGeometry.EdgesFor(index);

        Assert.Equal(w, edges.West);
        Assert.Equal(e, edges.East);
        Assert.Equal(n, edges.North);
        Assert.Equal(s, edges.South);
    }

    [Fact]
    public void DragEdges_MovesOnlyTheEdgesTheHandleOwns()
    {
        var start = new Rect(100, 100, 200, 150);

        // East handle: right edge moves, the other three stay put.
        var east = HandleGeometry.DragEdges(start, 4, 40, 999, Min, Bounds);

        Assert.Equal(start.X, east.X);
        Assert.Equal(start.Y, east.Y);
        Assert.Equal(start.Bottom, east.Bottom);
        Assert.Equal(start.Right + 40, east.Right);
    }

    [Fact]
    public void DragEdges_MovesLeadingEdges_ForNorthWestHandle()
    {
        var start = new Rect(100, 100, 200, 150);

        var nw = HandleGeometry.DragEdges(start, 0, 20, 10, Min, Bounds);

        Assert.Equal(120, nw.X);
        Assert.Equal(110, nw.Y);
        Assert.Equal(start.Right, nw.Right);      // opposite edges pinned
        Assert.Equal(start.Bottom, nw.Bottom);
    }

    // ---------------------------------------------------------------- clamping ---

    [Fact]
    public void DragEdges_HoldsTheRectInsideBounds()
    {
        var start = new Rect(100, 100, 200, 150);

        var result = HandleGeometry.DragEdges(start, 7, 9999, 9999, Min, Bounds);

        Assert.Equal(Bounds.Right, result.Right);
        Assert.Equal(Bounds.Bottom, result.Bottom);
    }

    [Fact]
    public void DragEdges_StopsAtMinimumSize_WhenDraggedPastTheOppositeEdge()
    {
        var start = new Rect(100, 100, 200, 150);

        var result = HandleGeometry.DragEdges(start, 0, 9999, 9999, Min, Bounds);

        Assert.Equal(Min, result.Width);
        Assert.Equal(Min, result.Height);
    }

    // ---------------------------------------------------------------- symmetric (Ctrl) ---

    [Theory]
    [MemberData(nameof(AllHandles))]
    public void DragEdgesSymmetric_KeepsTheCentreFixed(int handle)
    {
        var start = new Rect(300, 200, 200, 150);

        var result = HandleGeometry.DragEdgesSymmetric(start, handle, 30, -20, Min, Bounds);

        Assert.Equal(start.X + start.Width / 2, result.X + result.Width / 2);
        Assert.Equal(start.Y + start.Height / 2, result.Y + result.Height / 2);
    }

    [Theory]
    [InlineData(4, 30, 0, 270, 200, 260, 150)]    // E dragged out: W follows the other way
    [InlineData(3, 30, 0, 330, 200, 140, 150)]    // W dragged in: E follows inward
    [InlineData(1, 0, -20, 300, 180, 200, 190)]   // N dragged up: S goes down
    [InlineData(6, 0, -20, 300, 220, 200, 110)]   // S dragged up: N comes down
    [InlineData(7, 10, 10, 290, 190, 220, 170)]   // SE: both axes
    [InlineData(0, 10, 10, 310, 210, 180, 130)]   // NW dragged inward
    public void DragEdgesSymmetric_MovesTheOppositeEdgeByTheMirroredDelta(
        int handle, double dx, double dy, double x, double y, double w, double h)
    {
        var start = new Rect(300, 200, 200, 150);

        var result = HandleGeometry.DragEdgesSymmetric(start, handle, dx, dy, Min, Bounds);

        Assert.Equal(new Rect(x, y, w, h), result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void DragEdgesSymmetric_LeavesTheAxesAHandleDoesNotOwn(int handle)
    {
        var start = new Rect(300, 200, 200, 150);

        var result = HandleGeometry.DragEdgesSymmetric(start, handle, 999, 10, Min, Bounds);

        Assert.Equal(start.X, result.X);
        Assert.Equal(start.Width, result.Width);
    }

    [Theory]
    [InlineData(0, -50, -50)]
    [InlineData(1, 0, -50)]
    [InlineData(2, 50, -50)]
    [InlineData(3, -50, 0)]
    [InlineData(4, 50, 0)]
    [InlineData(5, -50, 50)]
    [InlineData(6, 0, 50)]
    [InlineData(7, 50, 50)]
    public void DragEdgesSymmetric_CannotGrowARectFillingTheBounds(int handle, double dx, double dy)
    {
        // Every handle dragged outward: the mirrored edge already sits on the bounds too.
        var result = HandleGeometry.DragEdgesSymmetric(Bounds, handle, dx, dy, Min, Bounds);

        Assert.Equal(Bounds, result);
    }

    [Fact]
    public void DragEdgesSymmetric_StopsBothEdges_WhenTheNearerOneReachesTheBounds()
    {
        // 100 px of room on the left, 700 on the right: the growth stops at 100 per side.
        var start = new Rect(100, 100, 200, 150);

        var result = HandleGeometry.DragEdgesSymmetric(start, 4, 500, 0, Min, Bounds);

        Assert.Equal(new Rect(0, 100, 400, 150), result);
    }

    [Theory]
    [MemberData(nameof(AllHandles))]
    public void DragEdgesSymmetric_StopsAtMinimumSize_AroundTheCentre(int handle)
    {
        var start = new Rect(300, 200, 200, 150);
        var (west, east, north, south) = HandleGeometry.EdgesFor(handle);
        // Drag every handle far inward, past the centre.
        double dx = west ? 9999 : -9999, dy = north ? 9999 : -9999;

        var result = HandleGeometry.DragEdgesSymmetric(start, handle, dx, dy, Min, Bounds);

        Assert.Equal(west || east ? Min : start.Width, result.Width);
        Assert.Equal(north || south ? Min : start.Height, result.Height);
        Assert.Equal(400, result.X + result.Width / 2);
        Assert.Equal(275, result.Y + result.Height / 2);
    }

    [Theory]
    [MemberData(nameof(AllHandles))]
    public void DragEdgesSymmetric_DoesNotThrow_WhenStartLiesOutsideShrunkenBounds(int handle)
    {
        var shrunk = new Rect(0, 0, 500, 50);
        var stranded = new Rect(900, 100, 60, 60);

        var result = HandleGeometry.DragEdgesSymmetric(stranded, handle, 5, 5, Min, shrunk);

        Assert.True(result.Width >= Min);
        Assert.True(result.Height >= Min);
    }

    [Fact]
    public void ClampInto_PullsAStrandedRectBackInside_KeepingItsSize()
    {
        var stranded = new Rect(900, 100, 60, 60);

        var result = HandleGeometry.ClampInto(stranded, new Rect(0, 0, 500, 400), Min);

        Assert.Equal(new Rect(440, 100, 60, 60), result);
    }

    [Fact]
    public void ClampInto_ShrinksARectTooLargeForTheBounds()
    {
        var huge = new Rect(0, 0, 900, 900);

        var result = HandleGeometry.ClampInto(huge, new Rect(0, 0, 500, 400), Min);

        Assert.Equal(new Rect(0, 0, 500, 400), result);
    }

    // ---------------------------------------------------------------- placement ---

    /// <summary>
    /// Regression: handles centred on the border cover a small framed area completely,
    /// leaving nothing to grab for moving it. Ringing the rect from outside keeps its
    /// interior free.
    /// </summary>
    [Fact]
    public void Place_Outside_LeavesTheRectInteriorCompletelyFree()
    {
        var small = new Rect(200, 200, 20, 14);
        const double handleSize = 12;

        var at = HandleGeometry.Place(small, handleSize, outside: true);

        double covered = 0;
        foreach (var p in at)
        {
            var handle = new Rect(p.X, p.Y, handleSize, handleSize);
            handle.Intersect(small);
            if (!handle.IsEmpty) covered += handle.Width * handle.Height;
        }
        Assert.Equal(0, covered);
    }

    [Fact]
    public void Place_Straddling_CentresEachHandleOnItsAnchor()
    {
        var r = new Rect(100, 100, 200, 150);
        const double handleSize = 12;

        var at = HandleGeometry.Place(r, handleSize, outside: false);

        // NW handle straddles the top-left corner.
        Assert.Equal(r.X - handleSize / 2, at[0].X);
        Assert.Equal(r.Y - handleSize / 2, at[0].Y);
        // SE handle straddles the bottom-right corner.
        Assert.Equal(r.Right - handleSize / 2, at[7].X);
        Assert.Equal(r.Bottom - handleSize / 2, at[7].Y);
    }

    [Fact]
    public void Place_ReturnsOnePositionPerHandle()
    {
        var at = HandleGeometry.Place(new Rect(0, 0, 50, 50), 12, outside: true);

        Assert.Equal(HandleGeometry.Count, at.Length);
    }
}
