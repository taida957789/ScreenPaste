namespace ScreenPaste.Editor;

/// <summary>
/// Shift+drag: a move restricted to one axis, the one the pointer has travelled further
/// along (horizontal on a tie). Used for the capture region; annotations can share it.
/// </summary>
public static class AxisLock
{
    /// <summary>(<paramref name="dx"/>, <paramref name="dy"/>) with the shorter axis zeroed.</summary>
    public static (double Dx, double Dy) Apply(double dx, double dy) =>
        Math.Abs(dx) >= Math.Abs(dy) ? (dx, 0) : (0, dy);
}
