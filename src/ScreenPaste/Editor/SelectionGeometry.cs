using System.Windows;

namespace ScreenPaste.Editor;

/// <summary>
/// Selection-phase geometry for the capture overlay, in overlay-local physical px: the
/// Shift+drag square, the Space-held anchor move and the Ctrl+A screen cycle. Free of any
/// WPF UI type, like <see cref="HandleGeometry"/>, so it is unit tested without a window.
/// </summary>
public static class SelectionGeometry
{
    /// <summary>
    /// The square a Shift+drag from <paramref name="start"/> to <paramref name="p"/> frames:
    /// the longer of the two drag distances on both axes, growing in the drag's direction
    /// and shrunk where needed so it stays inside <paramref name="bounds"/>.
    /// </summary>
    public static Rect Square(Point start, Point p, Rect bounds)
    {
        double dx = p.X - start.X, dy = p.Y - start.Y;
        bool right = dx >= 0, down = dy >= 0;

        double roomX = right ? bounds.Right - start.X : start.X - bounds.Left;
        double roomY = down ? bounds.Bottom - start.Y : start.Y - bounds.Top;
        double side = Math.Max(0, Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Min(roomX, roomY)));

        return new Rect(
            right ? start.X : start.X - side,
            down ? start.Y : start.Y - side,
            side, side);
    }

    /// <summary>A drag anchor shifted by <paramref name="delta"/> (Space held: the whole
    /// area travels with the pointer), kept on the screenshot.</summary>
    public static Point Translate(Point start, Vector delta, Rect bounds) => new(
        Math.Clamp(start.X + delta.X, bounds.Left, bounds.Right),
        Math.Clamp(start.Y + delta.Y, bounds.Top, bounds.Bottom));

    /// <summary>
    /// Ctrl+A: anything else goes to the cursor's monitor, the monitor grows to all screens
    /// (when there is more than one), and all screens go back to the monitor.
    /// </summary>
    public static Rect NextScreenRect(Rect? current, Rect cursorMonitor, Rect virtualRect, int monitorCount) =>
        monitorCount > 1 && current == cursorMonitor ? virtualRect : cursorMonitor;

    /// <summary>The monitor containing <paramref name="p"/> (a shared edge belongs to the
    /// monitor that starts there), else the nearest one, else <paramref name="fallback"/>.</summary>
    public static Rect MonitorAt(IReadOnlyList<Rect> monitors, Point p, Rect fallback)
    {
        Rect? nearest = null;
        double best = double.MaxValue;
        foreach (var m in monitors)
        {
            if (p.X >= m.Left && p.X < m.Right && p.Y >= m.Top && p.Y < m.Bottom) return m;

            double dx = Math.Max(0, Math.Max(m.Left - p.X, p.X - m.Right));
            double dy = Math.Max(0, Math.Max(m.Top - p.Y, p.Y - m.Bottom));
            double distance = dx * dx + dy * dy;
            if (distance < best) { best = distance; nearest = m; }
        }
        return nearest ?? fallback;
    }
}
