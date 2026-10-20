using System.Windows;

namespace ScreenPaste.Editor;

/// <summary>
/// Annotation geometry, in region-local px: Shift turns a rectangle, ellipse, blur or
/// magnifier drag into a square (a circle for the ellipse), and Shift+wheel scales a selected
/// rectangle or ellipse about its centre. Unlike <see cref="SelectionGeometry.Square"/> the
/// square is not held on the screenshot: annotations may run past the region, which clips them.
/// </summary>
public static class ShapeConstraints
{
    /// <summary>The square a Shift+drag from <paramref name="start"/> to <paramref name="p"/>
    /// frames: the longer of the two drag distances on both axes, in the drag's direction.</summary>
    public static Rect Square(Point start, Point p)
    {
        double dx = p.X - start.X, dy = p.Y - start.Y;
        double side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new Rect(
            dx >= 0 ? start.X : start.X - side,
            dy >= 0 ? start.Y : start.Y - side,
            side, side);
    }

    /// <summary>The rectangle a drag frames: the square when <paramref name="square"/>
    /// (Shift held), else the plain rectangle between the two points.</summary>
    public static Rect Frame(Point start, Point p, bool square) =>
        square ? Square(start, p) : new Rect(start, p);

    /// <summary>
    /// <paramref name="rect"/> scaled by <paramref name="factor"/> about its centre. Shrinking
    /// stops once the shorter side reaches <paramref name="minSize"/>, keeping the aspect
    /// ratio, so a shape already that small does not shrink at all; growing always works.
    /// </summary>
    public static Rect ScaleAbout(Rect rect, double factor, double minSize)
    {
        if (factor <= 0 || rect.IsEmpty) return rect;

        double shortest = Math.Min(rect.Width, rect.Height);
        if (factor < 1)
            factor = shortest > 0 ? Math.Max(factor, Math.Min(1, minSize / shortest)) : 1;
        if (factor == 1) return rect;

        double w = rect.Width * factor, h = rect.Height * factor;
        double cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        return new Rect(cx - w / 2, cy - h / 2, w, h);
    }
}
