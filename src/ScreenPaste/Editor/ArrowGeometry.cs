using System.Windows;
using System.Windows.Media;

namespace ScreenPaste.Editor;

/// <summary>
/// Geometry for the line/arrow annotation tool, shared by the capture and recording
/// editors. Render the result with a Path whose Stroke AND Fill are the line colour —
/// the stroke draws the segment, the fill closes the arrowhead triangles.
/// </summary>
public static class ArrowGeometry
{
    /// <summary>Line with optional filled arrowheads; the stroked segment is shortened
    /// under each head so a round line cap never pokes past the tip.</summary>
    public static Geometry Build(Point a, Point b, double width, bool arrowStart, bool arrowEnd)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        Vector d = b - a;
        double len = d.Length;
        if (len < 0.01)
        {
            group.Children.Add(new LineGeometry(a, b));
            return group;
        }
        d /= len;
        var perp = new Vector(-d.Y, d.X);
        // Desired head ≈ max(10, 3.5·width), but never longer than ~half the line —
        // computed with Min(Max(...)) because the two bounds can cross on short drags.
        double head = Math.Min(Math.Max(10, width * 3.5), len * 0.45);
        double halfW = head * 0.45;

        Point lineA = arrowStart ? a + d * (head * 0.8) : a;
        Point lineB = arrowEnd ? b - d * (head * 0.8) : b;
        group.Children.Add(new LineGeometry(lineA, lineB));

        if (arrowStart) group.Children.Add(Arrowhead(a, d, head, halfW, perp));
        if (arrowEnd) group.Children.Add(Arrowhead(b, -d, head, halfW, perp));
        return group;
    }

    /// <summary>Closed triangle with its apex at <paramref name="tip"/>, opening along
    /// <paramref name="inward"/> (unit vector pointing back along the line).</summary>
    private static Geometry Arrowhead(Point tip, Vector inward, double length, double halfWidth, Vector perp)
    {
        Point b1 = tip + inward * length + perp * halfWidth;
        Point b2 = tip + inward * length - perp * halfWidth;
        var figure = new PathFigure(tip,
            new PathSegment[] { new LineSegment(b1, true), new LineSegment(b2, true) },
            closed: true);
        return new PathGeometry(new[] { figure });
    }

    /// <summary>Snap the drag endpoint to the nearest 45° step around the start (Shift).</summary>
    public static Point SnapTo45(Point origin, Point p)
    {
        Vector v = p - origin;
        if (v.Length < 0.01) return p;
        double snapped = Math.Round(Math.Atan2(v.Y, v.X) / (Math.PI / 4)) * (Math.PI / 4);
        return origin + new Vector(Math.Cos(snapped), Math.Sin(snapped)) * v.Length;
    }
}
