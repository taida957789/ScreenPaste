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
