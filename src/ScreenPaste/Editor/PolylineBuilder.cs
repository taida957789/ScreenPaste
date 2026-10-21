using System.Windows;

namespace ScreenPaste.Editor;

/// <summary>
/// The line tool's strip mode, Snipaste-style: a press released without really moving (a
/// click) starts a strip at that point, each further click adds a point, and the last segment
/// follows the cursor until the strip is finished (right-click, Esc, Enter or a double-click).
/// Shift snaps each segment to 45 degrees about its own start. A strip of a single point, or
/// one too small to see, is dropped. Immutable, in region-local px; the window draws it with
/// <see cref="ArrowGeometry.BuildPolyline"/>.
/// </summary>
public sealed class PolylineBuilder
{
    /// <summary>A press released less than this far away is a click (the line tool's drag
    /// threshold): it starts a strip instead of drawing a line.</summary>
    public const double ClickTolerance = 8;
    /// <summary>A point this close to the last one is the same point: a double-click's second
    /// press, or a jittery click.</summary>
    public const double MinStep = 2;

    private readonly Point[] _points;

    private PolylineBuilder(Point[] points) => _points = points;

    public static PolylineBuilder Start(Point first) => new(new[] { first });

    public IReadOnlyList<Point> Points => _points;

    public Point Last => _points[^1];

    /// <summary>Fewer than two points, or all of them inside a box smaller than a click on
    /// both sides: nothing worth keeping.</summary>
    public bool IsDegenerate =>
        _points.Length < 2 || (Bounds.Width < ClickTolerance && Bounds.Height < ClickTolerance);

    /// <summary>The smallest rectangle holding every point.</summary>
    public Rect Bounds
    {
        get
        {
            var r = new Rect(_points[0], _points[0]);
            foreach (var p in _points) r.Union(p);
            return r;
        }
    }

    /// <summary>The strip with <paramref name="p"/> added (snapped to 45 degrees about the last
    /// point when <paramref name="snap"/>); unchanged when it lands on the last point.</summary>
    public PolylineBuilder Add(Point p, bool snap)
    {
        var at = Constrain(p, snap);
        if ((at - Last).Length < MinStep) return this;
        var points = new Point[_points.Length + 1];
        _points.CopyTo(points, 0);
        points[^1] = at;
        return new PolylineBuilder(points);
    }

    /// <summary>What to draw while the pointer is at <paramref name="cursor"/>: the points, then
    /// the segment from the last one to the (snapped) cursor.</summary>
    public IReadOnlyList<Point> Preview(Point cursor, bool snap) => Add(cursor, snap).Points;

    /// <summary>The finished strip's points, or null when it is degenerate.</summary>
    public IReadOnlyList<Point>? Finish() => IsDegenerate ? null : _points;

    /// <summary>True when a press at <paramref name="press"/> released at
    /// <paramref name="release"/> is a click rather than a drag.</summary>
    public static bool IsClick(Point press, Point release) => (release - press).Length < ClickTolerance;

    private Point Constrain(Point p, bool snap) => snap ? ArrowGeometry.SnapTo45(Last, p) : p;
}
