using System.Windows;
using System.Windows.Media;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The line strip's geometry: the stroked polyline through every point, with arrowheads at its
/// two ends only, and never a filled polygon, although the Path that draws it is filled (for
/// the arrowheads). Geometry objects are created on an STA thread like the rest of WPF.
/// </summary>
public sealed class ArrowGeometryTests
{
    private const double Width = 3;
    private static readonly Point[] Strip = { new(0, 0), new(100, 0), new(100, 100) };

    private static GeometryGroup Polyline(IReadOnlyList<Point> points, bool arrowStart, bool arrowEnd) =>
        Assert.IsType<GeometryGroup>(ArrowGeometry.BuildPolyline(points, Width, arrowStart, arrowEnd));

    [Fact]
    public void BuildPolyline_OfTwoPoints_IsTheSingleLine()
    {
        StaRunner.Run(() =>
        {
            var single = ArrowGeometry.Build(Strip[0], Strip[1], Width, false, true);

            var strip = ArrowGeometry.BuildPolyline(Strip[..2], Width, false, true);

            Assert.Equal(single.Bounds, strip.Bounds);
        });
    }

    [Fact]
    public void BuildPolyline_WithoutArrowheads_RunsThroughEveryPoint()
    {
        StaRunner.Run(() =>
        {
            var g = Polyline(Strip, arrowStart: false, arrowEnd: false);

            Assert.Single(g.Children);
            Assert.Equal(new Rect(0, 0, 100, 100), g.Bounds);
        });
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 3)]
    public void BuildPolyline_PutsArrowheadsAtTheEndsOnly(bool arrowStart, bool arrowEnd, int expectedParts)
    {
        StaRunner.Run(() =>
        {
            Assert.Equal(expectedParts, Polyline(Strip, arrowStart, arrowEnd).Children.Count);
        });
    }

    [Fact]
    public void BuildPolyline_IsNeverAFilledPolygon()
    {
        StaRunner.Run(() =>
        {
            // Inside the triangle the three points would close into.
            var inside = new Point(70, 30);

            Assert.False(Polyline(Strip, arrowStart: false, arrowEnd: false).FillContains(inside));
        });
    }

    [Fact]
    public void BuildPolyline_AimsTheEndArrowheadAlongTheLastSegment()
    {
        StaRunner.Run(() =>
        {
            var g = Polyline(Strip, arrowStart: false, arrowEnd: true);
            var head = g.Children[1].Bounds;

            // The last segment runs straight down to (100, 100): the head hangs above the tip,
            // centred on x = 100.
            Assert.Equal(100, head.Bottom, 6);
            Assert.Equal(100, head.X + head.Width / 2, 6);
            Assert.True(head.Top < 100);
        });
    }

    [Fact]
    public void BuildPolyline_OfAPointOrNothing_DoesNotThrow()
    {
        StaRunner.Run(() =>
        {
            Assert.NotNull(ArrowGeometry.BuildPolyline(new[] { new Point(5, 5) }, Width, true, true));
            Assert.NotNull(ArrowGeometry.BuildPolyline(Array.Empty<Point>(), Width, true, true));
        });
    }

    [Fact]
    public void BuildPolyline_SkipsTheArrowheadOfAZeroLengthEndSegment()
    {
        StaRunner.Run(() =>
        {
            var points = new[] { new Point(0, 0), new Point(50, 0), new Point(50, 0) };

            Assert.Single(Polyline(points, arrowStart: false, arrowEnd: true).Children);
        });
    }
}
