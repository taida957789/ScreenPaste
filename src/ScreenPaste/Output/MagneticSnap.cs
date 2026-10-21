using System.Windows;

namespace ScreenPaste.Output;

/// <summary>
/// Shift+drag on a pin (Snipaste's magnetic attach): an edge that comes within the threshold
/// of an edge of another pin, a window or a screen jumps onto it, lining up with it (left to
/// left) or butting against it (left to right). Each axis snaps on its own, so a corner snaps
/// when both do, and the nearest edge wins. An edge only pulls where the two rects overlap
/// (give or take the threshold) along the other axis, so a window far below does not drag
/// the pin sideways. Pure, so it is unit tested; physical screen px.
/// </summary>
public static class MagneticSnap
{
    /// <summary><paramref name="rect"/> moved onto the nearest edges of
    /// <paramref name="candidates"/> within <paramref name="threshold"/>; its size never changes.</summary>
    public static Rect Snap(Rect rect, IReadOnlyList<Rect> candidates, double threshold)
    {
        if (rect.IsEmpty || threshold <= 0) return rect;

        double? dx = null, dy = null;
        foreach (var c in candidates)
        {
            if (c.IsEmpty) continue;
            if (Overlaps(rect.Top, rect.Bottom, c.Top, c.Bottom, threshold))
                dx = Nearer(dx, Pull(rect.Left, rect.Right, c.Left, c.Right, threshold));
            if (Overlaps(rect.Left, rect.Right, c.Left, c.Right, threshold))
                dy = Nearer(dy, Pull(rect.Top, rect.Bottom, c.Top, c.Bottom, threshold));
        }
        return new Rect(rect.X + (dx ?? 0), rect.Y + (dy ?? 0), rect.Width, rect.Height);
    }

    /// <summary>The shortest move, within the threshold, that puts one of the rect's two
    /// edges on one of the candidate's; null when none is that close.</summary>
    private static double? Pull(double start, double end, double otherStart, double otherEnd, double threshold)
    {
        double? best = null;
        foreach (double edge in new[] { start, end })
        {
            foreach (double target in new[] { otherStart, otherEnd })
            {
                double move = target - edge;
                if (Math.Abs(move) <= threshold) best = Nearer(best, move);
            }
        }
        return best;
    }

    private static double? Nearer(double? current, double? candidate) =>
        candidate is { } c && (current is not { } now || Math.Abs(c) < Math.Abs(now)) ? c : current;

    /// <summary>Whether two spans along one axis overlap or come within the threshold.</summary>
    private static bool Overlaps(double start, double end, double otherStart, double otherEnd, double threshold) =>
        start <= otherEnd + threshold && end >= otherStart - threshold;
}
