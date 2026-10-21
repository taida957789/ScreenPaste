using System.Windows;

namespace ScreenPaste.Output;

/// <summary>
/// Which pin is under a point, for the click-through hotkey: the pins' window rects in
/// physical screen px, topmost first, and the first that contains the point. The left and
/// top edges are inside a rect, the right and bottom ones outside, as GetWindowRect has it.
/// Pure, so it is unit tested.
/// </summary>
public static class PinHitTest
{
    /// <summary>The index of the topmost rect containing <paramref name="point"/>, or null.</summary>
    public static int? Find(IReadOnlyList<Rect> rectsInZOrder, Point point)
    {
        for (int i = 0; i < rectsInZOrder.Count; i++)
        {
            var r = rectsInZOrder[i];
            if (r.IsEmpty) continue;
            if (point.X >= r.Left && point.X < r.Right && point.Y >= r.Top && point.Y < r.Bottom) return i;
        }
        return null;
    }
}
