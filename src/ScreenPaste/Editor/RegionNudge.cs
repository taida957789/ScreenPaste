using System.Windows;
using System.Windows.Input;

namespace ScreenPaste.Editor;

/// <summary>
/// Snipaste's arrow keys once the capture region is chosen, a pixel per press: plain arrows
/// move the region, Ctrl+arrows grow the side the arrow points to, and Shift+arrows move the
/// opposite side in the arrow's direction (shrinking it). Kept on the screenshot and never
/// below the minimum size; resizing goes through <see cref="HandleGeometry.DragEdges"/>, so
/// it shares the handles' tolerance for inverted limits.
/// </summary>
public static class RegionNudge
{
    /// <summary>One press, in physical px.</summary>
    public const double Step = 1;

    // Handle indices (HandleGeometry): 1=N 3=W 4=E 6=S.
    private const int North = 1, West = 3, East = 4, South = 6;

    /// <summary><paramref name="rect"/> after <paramref name="key"/>; unchanged for any key
    /// other than an arrow, or an arrow held with other modifiers.</summary>
    public static Rect Apply(Rect rect, Key key, ModifierKeys modifiers, Rect bounds, double minSize)
    {
        var (dx, dy) = Direction(key);
        if (dx == 0 && dy == 0) return rect;

        if (modifiers == ModifierKeys.None)
            return HandleGeometry.ClampInto(Rect.Offset(rect, dx * Step, dy * Step), bounds, minSize);
        if (modifiers == ModifierKeys.Control)
            return HandleGeometry.DragEdges(rect, GrowHandle(key), dx * Step, dy * Step, minSize, bounds);
        if (modifiers == ModifierKeys.Shift)
            return HandleGeometry.DragEdges(rect, ShrinkHandle(key), dx * Step, dy * Step, minSize, bounds);
        return rect;
    }

    private static (int Dx, int Dy) Direction(Key key) => key switch
    {
        Key.Up => (0, -1),
        Key.Down => (0, 1),
        Key.Left => (-1, 0),
        Key.Right => (1, 0),
        _ => (0, 0),
    };

    /// <summary>Growing drags the edge the arrow points at outward.</summary>
    private static int GrowHandle(Key key) => key switch
    {
        Key.Up => North,
        Key.Down => South,
        Key.Left => West,
        _ => East,
    };

    /// <summary>Shrinking drags the opposite edge inward, in the arrow's direction.</summary>
    private static int ShrinkHandle(Key key) => key switch
    {
        Key.Up => South,
        Key.Down => North,
        Key.Left => East,
        _ => West,
    };
}
