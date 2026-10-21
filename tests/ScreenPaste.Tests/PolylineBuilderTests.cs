using System.Windows;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The line tool's strip mode: a short click starts a strip, every further click adds a
/// point (Shift snaps that segment to 45 degrees about its own start), the last segment
/// follows the cursor, and a strip of a single point, or one too small to see, is dropped.
/// </summary>
public sealed class PolylineBuilderTests
{
    private const int Precision = 6;
    private static readonly Point Origin = new(10, 20);

    // ---------------------------------------------------------------- start ---

    [Fact]
    public void Start_HoldsTheFirstPoint_AndIsNotYetALine()
    {
        var strip = PolylineBuilder.Start(Origin);

        Assert.Equal(new[] { Origin }, strip.Points);
        Assert.Equal(Origin, strip.Last);
        Assert.True(strip.IsDegenerate);
    }

    // ---------------------------------------------------------------- adding ---

    [Fact]
    public void Add_AppendsThePoint()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(60, 20), snap: false).Add(new Point(60, 90), snap: false);

        Assert.Equal(new[] { Origin, new Point(60, 20), new Point(60, 90) }, strip.Points);
        Assert.False(strip.IsDegenerate);
    }

    [Fact]
    public void Add_LeavesTheEarlierStripAlone()
    {
        var start = PolylineBuilder.Start(Origin);

        var longer = start.Add(new Point(80, 20), snap: false);

        Assert.Single(start.Points);
        Assert.Equal(2, longer.Points.Count);
    }

    [Fact]
    public void Add_WithSnap_PutsTheSegmentOnAMultipleOf45Degrees()
    {
        // 100 right and 10 down is nearest to horizontal.
        var strip = PolylineBuilder.Start(Origin).Add(new Point(Origin.X + 100, Origin.Y + 10), snap: true);

        Assert.Equal(Origin.Y, strip.Last.Y, Precision);
        Assert.True(strip.Last.X > Origin.X);
    }

    [Fact]
    public void Add_WithSnap_SnapsEachSegmentAboutItsOwnStart()
    {
        // Arrange: a first, unsnapped segment that ends off the start's 45-degree lines.
        var corner = new Point(73, 41);
        var strip = PolylineBuilder.Start(Origin).Add(corner, snap: false);

        // Act: nearly diagonal from the corner.
        strip = strip.Add(new Point(corner.X + 50, corner.Y + 46), snap: true);

        // Assert: diagonal about the corner, not about the strip's start.
        var last = strip.Last;
        Assert.Equal(last.X - corner.X, last.Y - corner.Y, Precision);
        Assert.Equal(corner, strip.Points[1]);
    }

    [Fact]
    public void Add_WithoutSnap_KeepsThePointAsClicked()
    {
        var p = new Point(73, 41);

        Assert.Equal(p, PolylineBuilder.Start(Origin).Add(p, snap: false).Last);
    }

    [Fact]
    public void Add_TheSamePointAgain_IsIgnored()
    {
        // A double-click's second press lands where the first one added a point.
        var strip = PolylineBuilder.Start(Origin).Add(new Point(80, 20), snap: false);

        var again = strip.Add(new Point(80.5, 20.5), snap: false);

        Assert.Equal(strip.Points, again.Points);
    }

    // ---------------------------------------------------------------- preview ---

    [Fact]
    public void Preview_EndsAtTheCursor_WithoutAddingIt()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(80, 20), snap: false);
        var cursor = new Point(95, 70);

        var preview = strip.Preview(cursor, snap: false);

        Assert.Equal(new[] { Origin, new Point(80, 20), cursor }, preview);
        Assert.Equal(2, strip.Points.Count);
    }

    [Fact]
    public void Preview_WithSnap_SnapsTheSegmentToTheCursor()
    {
        var strip = PolylineBuilder.Start(Origin);

        var preview = strip.Preview(new Point(Origin.X + 4, Origin.Y + 90), snap: true);

        Assert.Equal(Origin.X, preview[^1].X, Precision);
    }

    [Fact]
    public void Preview_AtTheLastPoint_IsJustThePoints()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(80, 20), snap: false);

        Assert.Equal(strip.Points, strip.Preview(new Point(80, 20), snap: false));
    }

    // ---------------------------------------------------------------- finishing ---

    [Fact]
    public void Finish_WithASinglePoint_IsDiscarded()
    {
        Assert.Null(PolylineBuilder.Start(Origin).Finish());
    }

    [Fact]
    public void Finish_WithTwoOrMorePoints_GivesThePoints()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(80, 20), snap: false).Add(new Point(80, 90), snap: false);

        Assert.Equal(strip.Points, strip.Finish());
    }

    [Fact]
    public void Finish_WithAStripTooSmallToSee_IsDiscarded()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(Origin.X + 4, Origin.Y + 3), snap: false);

        Assert.True(strip.IsDegenerate);
        Assert.Null(strip.Finish());
    }

    [Fact]
    public void Finish_WithAStraightStripLongEnoughOnOneAxis_IsKept()
    {
        var strip = PolylineBuilder.Start(Origin).Add(new Point(Origin.X + 40, Origin.Y), snap: false);

        Assert.NotNull(strip.Finish());
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 5, true)]
    [InlineData(7.9, 0, true)]
    [InlineData(8, 0, false)]
    [InlineData(30, 12, false)]
    public void IsClick_IsAPressReleasedWithoutReallyMoving(double dx, double dy, bool expected)
    {
        Assert.Equal(expected, PolylineBuilder.IsClick(Origin, new Point(Origin.X + dx, Origin.Y + dy)));
    }

    // ---------------------------------------------------------------- bounds ---

    [Fact]
    public void Bounds_CoversEveryPoint()
    {
        var strip = PolylineBuilder.Start(new Point(50, 60))
            .Add(new Point(10, 90), snap: false)
            .Add(new Point(120, 30), snap: false);

        Assert.Equal(new Rect(10, 30, 110, 60), strip.Bounds);
    }

    [Fact]
    public void Bounds_OfASinglePoint_IsThatPoint()
    {
        Assert.Equal(new Rect(Origin, Origin), PolylineBuilder.Start(Origin).Bounds);
    }
}
