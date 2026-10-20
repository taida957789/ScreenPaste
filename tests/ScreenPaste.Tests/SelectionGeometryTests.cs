using System.Windows;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Selection-phase geometry: the Shift+drag square, the Space-held anchor move and the
/// Ctrl+A screen cycle (active screen, then all screens). All in overlay-local physical px.
/// </summary>
public sealed class SelectionGeometryTests
{
    private static readonly Rect Bounds = new(0, 0, 1000, 800);

    // Two side-by-side 1920x1080 monitors; the virtual screen is their bounding box.
    private static readonly Rect Left = new(0, 0, 1920, 1080);
    private static readonly Rect Right = new(1920, 0, 1920, 1080);
    private static readonly Rect Virtual = new(0, 0, 3840, 1080);

    // ---------------------------------------------------------------- Square ---

    [Theory]
    [InlineData(600, 450, 500, 400, 100)]   // down-right, dx dominant
    [InlineData(550, 500, 500, 400, 100)]   // down-right, dy dominant
    [InlineData(400, 450, 400, 400, 100)]   // down-left
    [InlineData(600, 350, 500, 300, 100)]   // up-right
    [InlineData(420, 300, 400, 300, 100)]   // up-left, dy dominant
    public void Square_TakesTheLongerSide_InTheDragDirection(
        double px, double py, double x, double y, double side)
    {
        var start = new Point(500, 400);

        var r = SelectionGeometry.Square(start, new Point(px, py), Bounds);

        Assert.Equal(new Rect(x, y, side, side), r);
    }

    [Fact]
    public void Square_StopsAtTheNearerScreenEdge_AndStaysSquare()
    {
        // 50 px of room to the right, plenty below: the square cannot outgrow 50.
        var start = new Point(950, 100);

        var r = SelectionGeometry.Square(start, new Point(980, 400), Bounds);

        Assert.Equal(new Rect(950, 100, 50, 50), r);
    }

    [Fact]
    public void Square_StopsAtTheTopLeftEdges_ForAnUpLeftDrag()
    {
        var start = new Point(30, 300);

        var r = SelectionGeometry.Square(start, new Point(0, 0), Bounds);

        Assert.Equal(new Rect(0, 270, 30, 30), r);
    }

    [Fact]
    public void Square_WithNoMovement_IsEmptyAtTheStart()
    {
        var r = SelectionGeometry.Square(new Point(200, 200), new Point(200, 200), Bounds);

        Assert.Equal(new Rect(200, 200, 0, 0), r);
    }

    [Fact]
    public void Square_NeverGoesNegative_WhenTheStartLiesOutsideTheBounds()
    {
        var r = SelectionGeometry.Square(new Point(1200, 100), new Point(1300, 200), Bounds);

        Assert.Equal(0, r.Width);
        Assert.Equal(0, r.Height);
    }

    // ---------------------------------------------------------------- Translate ---

    [Fact]
    public void Translate_MovesTheAnchorByTheDelta()
    {
        var moved = SelectionGeometry.Translate(new Point(100, 200), new Vector(15, -20), Bounds);

        Assert.Equal(new Point(115, 180), moved);
    }

    [Theory]
    [InlineData(-500, 0, 0, 200)]
    [InlineData(5000, 0, 1000, 200)]
    [InlineData(0, -900, 100, 0)]
    [InlineData(0, 900, 100, 800)]
    public void Translate_KeepsTheAnchorOnTheScreenshot(double dx, double dy, double x, double y)
    {
        var moved = SelectionGeometry.Translate(new Point(100, 200), new Vector(dx, dy), Bounds);

        Assert.Equal(new Point(x, y), moved);
    }

    // ---------------------------------------------------------------- NextScreenRect ---

    [Fact]
    public void NextScreenRect_FromNothing_PicksTheCursorsMonitor()
    {
        Assert.Equal(Right, SelectionGeometry.NextScreenRect(null, Right, Virtual, 2));
    }

    [Fact]
    public void NextScreenRect_FromAnArbitraryArea_PicksTheCursorsMonitor()
    {
        var area = new Rect(10, 10, 300, 200);

        Assert.Equal(Left, SelectionGeometry.NextScreenRect(area, Left, Virtual, 2));
    }

    [Fact]
    public void NextScreenRect_FromTheMonitor_GrowsToAllScreens()
    {
        Assert.Equal(Virtual, SelectionGeometry.NextScreenRect(Left, Left, Virtual, 2));
    }

    [Fact]
    public void NextScreenRect_FromAllScreens_GoesBackToTheMonitor()
    {
        Assert.Equal(Left, SelectionGeometry.NextScreenRect(Virtual, Left, Virtual, 2));
    }

    [Fact]
    public void NextScreenRect_WithOneMonitor_StaysOnIt()
    {
        var only = new Rect(0, 0, 1920, 1080);

        Assert.Equal(only, SelectionGeometry.NextScreenRect(null, only, only, 1));
        Assert.Equal(only, SelectionGeometry.NextScreenRect(only, only, only, 1));
    }

    // ---------------------------------------------------------------- MonitorAt ---

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(1919, 500, 0)]
    [InlineData(1920, 500, 1)]   // a shared edge belongs to the monitor that starts there
    [InlineData(3000, 1079, 1)]
    public void MonitorAt_FindsTheMonitorUnderThePoint(double x, double y, int expected)
    {
        var monitors = new[] { Left, Right };

        Assert.Equal(monitors[expected], SelectionGeometry.MonitorAt(monitors, new Point(x, y), Virtual));
    }

    [Fact]
    public void MonitorAt_InADeadZone_FallsBackToTheNearestMonitor()
    {
        // A 1280x720 monitor right of a 1920x1080 one leaves a gap below it.
        var small = new Rect(1920, 0, 1280, 720);
        var monitors = new[] { Left, small };

        Assert.Equal(small, SelectionGeometry.MonitorAt(monitors, new Point(2500, 1000), Virtual));
    }

    [Fact]
    public void MonitorAt_WithNoMonitors_ReturnsTheFallback()
    {
        Assert.Equal(Virtual, SelectionGeometry.MonitorAt(Array.Empty<Rect>(), new Point(5, 5), Virtual));
    }
}
