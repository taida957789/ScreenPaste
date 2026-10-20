using System.Windows;

namespace ScreenPaste.Editor;

/// <summary>
/// Geometry for the 8 resize handles, shared by the capture region, a magnifier's framed
/// source and a pin's border (which scales it, keeping its shape). Handle indices are
/// <c>0=NW 1=N 2=NE 3=W 4=E 5=SW 6=S 7=SE</c>.
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

    /// <summary>
    /// Ctrl+drag of a handle: resize about the centre. The edges handle
    /// <paramref name="index"/> owns move by the drag, and the opposite edges by the mirrored
    /// amount, so the centre stays put. Growth stops when either edge of an axis reaches
    /// <paramref name="bounds"/> and shrinking stops at <paramref name="minSize"/>; like
    /// <see cref="DragEdges"/>, inverted limits collapse to the minimum instead of throwing.
    /// </summary>
    public static Rect DragEdgesSymmetric(Rect start, int index, double dx, double dy,
        double minSize, Rect bounds)
    {
        var (west, east, north, south) = EdgesFor(index);

        // Growth per side: dragging an east/south edge out is +delta, a west/north one -delta.
        double gx = west || east
            ? SymmetricGrowth(east ? dx : -dx, start.Width,
                start.Left - bounds.Left, bounds.Right - start.Right, minSize)
            : 0;
        double gy = north || south
            ? SymmetricGrowth(south ? dy : -dy, start.Height,
                start.Top - bounds.Top, bounds.Bottom - start.Bottom, minSize)
            : 0;

        return new Rect(start.X - gx, start.Y - gy,
            Math.Max(minSize, start.Width + 2 * gx),
            Math.Max(minSize, start.Height + 2 * gy));
    }

    /// <summary>One axis of <see cref="DragEdgesSymmetric"/>: the growth applied to each side,
    /// at least what keeps <paramref name="minSize"/> and at most the room on the tighter side.</summary>
    private static double SymmetricGrowth(double growth, double size, double roomBefore,
        double roomAfter, double minSize) =>
        ClampTolerant(growth, (minSize - size) / 2, Math.Min(roomBefore, roomAfter));

    /// <summary>
    /// A pin's border drag: scale <paramref name="start"/> keeping its width-to-height ratio.
    /// A corner handle scales by whichever of its two edges moved further (relative to its
    /// side) and keeps the opposite corner; an edge handle keeps the opposite edge and stays
    /// centred along it (<see cref="ResizeAnchored"/>). The shorter side never drops below
    /// <paramref name="minSize"/>; an empty rect is left as it is. Unbounded: the caller
    /// clamps the result (a pin's zoom range).
    /// </summary>
    public static Rect DragEdgesAspect(Rect start, int index, double dx, double dy, double minSize)
    {
        if (start.IsEmpty || start.Width <= 0 || start.Height <= 0) return start;
        var (west, east, north, south) = EdgesFor(index);

        // The scale each owned axis asks for: dragging an east/south edge out is +delta.
        double? sx = west || east ? (start.Width + (east ? dx : -dx)) / start.Width : null;
        double? sy = north || south ? (start.Height + (south ? dy : -dy)) / start.Height : null;
        double scale = (sx, sy) switch
        {
            ({ } x, { } y) => Math.Abs(x - 1) >= Math.Abs(y - 1) ? x : y,
            ({ } x, null) => x,
            (null, { } y) => y,
            _ => 1,
        };
        scale = Math.Max(scale, minSize / Math.Min(start.Width, start.Height));
        return ResizeAnchored(start, index, start.Width * scale, start.Height * scale);
    }

    /// <summary>
    /// <paramref name="start"/> at a new size, kept where handle <paramref name="index"/>
    /// does not drag: a corner handle keeps the opposite corner, an edge handle the opposite
    /// edge with the rect centred along it.
    /// </summary>
    public static Rect ResizeAnchored(Rect start, int index, double width, double height)
    {
        var (west, east, north, south) = EdgesFor(index);
        double x = west ? start.Right - width : east ? start.Left : start.X + (start.Width - width) / 2;
        double y = north ? start.Bottom - height : south ? start.Top : start.Y + (start.Height - height) / 2;
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// The handle a pin's border offers at <paramref name="p"/> (physical px, as
    /// GetWindowRect gives the rect: the right and bottom edges outside): an edge within
    /// <paramref name="band"/> px of it, a corner where the point is near one edge and within
    /// twice the band of the other, so corners are easier to catch. Null inside or outside.
    /// </summary>
    public static int? BorderHandleAt(Rect r, Point p, double band)
    {
        if (r.IsEmpty || band <= 0) return null;
        if (p.X < r.Left || p.X >= r.Right || p.Y < r.Top || p.Y >= r.Bottom) return null;

        // Distances in whole pixels from each edge's own pixel row or column.
        double dl = p.X - r.Left, dr = r.Right - 1 - p.X, dt = p.Y - r.Top, db = r.Bottom - 1 - p.Y;
        bool w = dl < band, e = dr < band, n = dt < band, s = db < band;
        if (!(w || e || n || s)) return null;

        double reach = band * 2;
        if ((w && dt < reach) || (n && dl < reach)) return 0;   // NW
        if ((e && dt < reach) || (n && dr < reach)) return 2;   // NE
        if ((w && db < reach) || (s && dl < reach)) return 5;   // SW
        if ((e && db < reach) || (s && dr < reach)) return 7;   // SE
        if (n) return 1;
        if (s) return 6;
        return w ? 3 : 4;
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
