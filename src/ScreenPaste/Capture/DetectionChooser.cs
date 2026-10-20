using System.Windows;

namespace ScreenPaste.Capture;

/// <summary>What hovering highlights while framing: the UI element under the cursor
/// (walked outward with the wheel) or always the whole window. Tab switches.</summary>
public enum DetectMode { Element, Window }

/// <summary>
/// Picks the rect to highlight from the candidate chain under the cursor
/// (<see cref="ElementDetector.CandidatesAt"/>): smallest first, always ending with the
/// window itself, so index 0 is the deepest element and the last entry is the window.
/// Pure, so the Tab / wheel rules are unit tested.
/// </summary>
public static class DetectionChooser
{
    /// <summary>The highlighted rect, or null for an empty chain. Window mode ignores the
    /// level; element mode clamps it into the chain.</summary>
    public static Rect? Pick(IReadOnlyList<Rect> chain, DetectMode mode, int level)
    {
        if (chain.Count == 0) return null;
        return mode == DetectMode.Window
            ? chain[^1]
            : chain[Math.Clamp(level, 0, chain.Count - 1)];
    }

    public static DetectMode Toggle(DetectMode mode) =>
        mode == DetectMode.Element ? DetectMode.Window : DetectMode.Element;

    /// <summary>
    /// One wheel notch over a chain of <paramref name="count"/> candidates. Snipaste-style,
    /// scrolling in window mode switches back to element detection; it starts from the
    /// window level so the highlight does not jump, and the notch then applies.
    /// </summary>
    public static (DetectMode Mode, int Level) Wheel(DetectMode mode, int level, int count, bool outward)
    {
        int top = Math.Max(0, count - 1);
        int from = mode == DetectMode.Window ? top : level;
        return (DetectMode.Element, Math.Clamp(from + (outward ? 1 : -1), 0, top));
    }
}
