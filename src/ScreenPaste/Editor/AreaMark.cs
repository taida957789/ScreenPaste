using System.Windows;
using ScreenPaste.Capture;

namespace ScreenPaste.Editor;

/// <summary>
/// A right-click with a fill tool (shape, blur, eraser) marks the UI element under the cursor:
/// the deepest element of the detection chain (<see cref="DetectionChooser"/>), turned from
/// screen px into region-local px and cut to the region. Pure, so the clipping is unit tested.
/// </summary>
public static class AreaMark
{
    /// <summary>The deepest element of <paramref name="chain"/> (screen px, smallest first, as
    /// <see cref="ElementDetector.CandidatesAt"/> returns it) within <paramref name="region"/>,
    /// or null when nothing usable is there.</summary>
    public static Rect? ElementInRegion(IReadOnlyList<Rect> chain, Rect region, double minSize) =>
        DetectionChooser.Pick(chain, DetectMode.Element, 0) is { } element
            ? Clip(element, region, minSize)
            : null;

    /// <summary>
    /// <paramref name="rect"/> (screen px) cut to <paramref name="region"/> (screen px) and made
    /// relative to the region's top-left. Null when what is left is thinner than
    /// <paramref name="minSize"/> on either side, which includes not overlapping at all.
    /// </summary>
    public static Rect? Clip(Rect rect, Rect region, double minSize)
    {
        var r = Rect.Intersect(rect, region);
        if (r.IsEmpty || r.Width < minSize || r.Height < minSize) return null;
        return new Rect(r.X - region.X, r.Y - region.Y, r.Width, r.Height);
    }
}
