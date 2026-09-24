using System.Windows;

namespace ScreenPaste.Editor;

/// <summary>
/// Geometry for the 8 resize handles, shared by the capture region and by a magnifier's
/// framed source. Handle indices are <c>0=NW 1=N 2=NE 3=W 4=E 5=SW 6=S 7=SE</c>.
///
/// Deliberately free of any WPF UI type so the arithmetic can be unit tested without a
/// dispatcher — the edge maths used to live inline in two mouse handlers, where it was
/// unreachable by tests and had drifted into two copies of the same index convention.
/// </summary>
public static class HandleGeometry
{
    public const int Count = 8;

    /// <summary>Which edges of the rect the given handle drags.</summary>
    public static (bool West, bool East, bool North, bool South) EdgesFor(int index) => (
        West: index is 0 or 3 or 5,
        East: index is 2 or 4 or 7,
        North: index is 0 or 1 or 2,
        South: index is 5 or 6 or 7);

    /// <summary>
    /// <paramref name="start"/> after dragging handle <paramref name="index"/> by
    /// (<paramref name="dx"/>,<paramref name="dy"/>): only the edges that handle owns move,
    /// held inside <paramref name="bounds"/> and never thinner than
    /// <paramref name="minSize"/>.
    /// </summary>
    /// <remarks>
    /// The clamp limits go through <see cref="ClampTolerant"/> rather than
    /// <see cref="Math.Clamp"/> because they CAN invert: <paramref name="start"/> may already
    /// lie outside <paramref name="bounds"/> when the capture region was resized smaller
    /// underneath an existing annotation. <see cref="Math.Clamp"/> throws on inverted limits,
    /// which previously took down the whole capture session.
    /// </remarks>
    public static Rect DragEdges(Rect start, int index, double dx, double dy,
        double minSize, Rect bounds)
    {
        var (west, east, north, south) = EdgesFor(index);

        double left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;

        if (west) left = ClampTolerant(left + dx, bounds.Left, right - minSize);
        if (east) right = ClampTolerant(right + dx, left + minSize, bounds.Right);
        if (north) top = ClampTolerant(top + dy, bounds.Top, bottom - minSize);
        if (south) bottom = ClampTolerant(bottom + dy, top + minSize, bounds.Bottom);

        return new Rect(left, top,
            Math.Max(minSize, right - left),
            Math.Max(minSize, bottom - top));
    }

    /// <summary>Move <paramref name="r"/> back inside <paramref name="bounds"/>, keeping its
    /// size; it is shrunk only when it is too large to fit.</summary>
    public static Rect ClampInto(Rect r, Rect bounds, double minSize)
    {
        double w = Math.Clamp(r.Width, minSize, Math.Max(minSize, bounds.Width));
        double h = Math.Clamp(r.Height, minSize, Math.Max(minSize, bounds.Height));
        return new Rect(
            ClampTolerant(r.X, bounds.Left, bounds.Right - w),
            ClampTolerant(r.Y, bounds.Top, bounds.Bottom - h),
            w, h);
    }

    /// <summary>
    /// Top-left positions for 8 handles of <paramref name="handleSize"/> px around
    /// <paramref name="r"/>. <paramref name="outside"/> rings them just outside the rect,
    /// leaving its interior free to grab — which matters for a small rect, where straddling
    /// handles would cover it completely; otherwise they straddle its border.
    /// </summary>
    public static Point[] Place(Rect r, double handleSize, bool outside)
    {
        double half = handleSize / 2.0;
        double x = r.X, y = r.Y, w = r.Width, h = r.Height;

        // Straddling centres each handle on the edge; "outside" pushes it fully clear of it.
        double lead = outside ? handleSize : half;
        double trail = outside ? 0 : half;

        return new[]
        {
            new Point(x - lead,         y - lead),          // NW
            new Point(x + w / 2 - half, y - lead),          // N
            new Point(x + w - trail,    y - lead),          // NE
            new Point(x - lead,         y + h / 2 - half),  // W
            new Point(x + w - trail,    y + h / 2 - half),  // E
            new Point(x - lead,         y + h - trail),     // SW
            new Point(x + w / 2 - half, y + h - trail),     // S
            new Point(x + w - trail,    y + h - trail),     // SE
        };
    }

    /// <summary>Clamp that yields <paramref name="min"/> on inverted limits instead of
    /// throwing, so an out-of-bounds rect collapses to its minimum rather than crashing.</summary>
    private static double ClampTolerant(double value, double min, double max) =>
        max < min ? min : Math.Clamp(value, min, max);
}
