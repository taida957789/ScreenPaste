using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// UI-element detection for the selection phase (Snipaste-style), built on UI Automation.
/// Because the capture freezes the screen, the UIA trees of the visible windows are
/// walked ONCE on a background thread right after the overlay opens; hovering then
/// hit-tests the cached rectangles locally with zero latency. Until a window's scan
/// finishes (or if its provider fails — e.g. elevated windows), detection gracefully
/// degrades to the whole-window rectangle.
/// </summary>
internal sealed class ElementDetector
{
    private const int MaxElementsPerWindow = 3000;  // huge trees (browsers) get truncated
    private const int MinElementSize = 8;           // physical px; skip specks
    private static readonly TimeSpan ScanBudget = TimeSpan.FromSeconds(5);

    private readonly Dictionary<IntPtr, List<Rect>> _cache = new();   // screen physical px
    private readonly object _lock = new();
    private volatile bool _cancelled;

    /// <summary>Walk the UIA tree of each window (topmost first) on a background thread.</summary>
    public void StartScan(IReadOnlyList<DetectedWindow> windows)
    {
        var snapshot = windows.ToArray();
        Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            foreach (var w in snapshot)
            {
                if (_cancelled || sw.Elapsed > ScanBudget) break;
                var rects = ScanWindow(w);
                lock (_lock) _cache[w.Handle] = rects;
            }
        });
    }

    public void Cancel() => _cancelled = true;

    /// <summary>
    /// Candidate rectangles under a physical-px point for the given window, smallest
    /// first, always ending with the window rectangle itself — so index 0 is the deepest
    /// element and higher indices walk outward (mouse-wheel level cycling).
    /// </summary>
    public List<Rect> CandidatesAt(DetectedWindow window, int screenX, int screenY)
    {
        var result = new List<Rect>();

        List<Rect>? rects;
        lock (_lock) _cache.TryGetValue(window.Handle, out rects);
        if (rects != null)
        {
            foreach (var r in rects)
                if (screenX >= r.X && screenX < r.Right && screenY >= r.Y && screenY < r.Bottom)
                    result.Add(r);
            result.Sort((a, b) => (a.Width * a.Height).CompareTo(b.Width * b.Height));

            // Collapse near-identical nesting levels (borders/padding wrappers).
            for (int i = result.Count - 1; i > 0; i--)
                if (NearlyEqual(result[i], result[i - 1]))
                    result.RemoveAt(i);
        }

        var b = window.Bounds;
        var winRect = new Rect(b.Left, b.Top, b.Width, b.Height);
        if (result.Count == 0 || !NearlyEqual(result[^1], winRect))
            result.Add(winRect);
        return result;
    }

    private static bool NearlyEqual(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < 3 && Math.Abs(a.Y - b.Y) < 3 &&
        Math.Abs(a.Right - b.Right) < 3 && Math.Abs(a.Bottom - b.Bottom) < 3;

    private List<Rect> ScanWindow(DetectedWindow w)
    {
        var list = new List<Rect>();
        try
        {
            var winRect = new Rect(w.Bounds.Left, w.Bounds.Top, w.Bounds.Width, w.Bounds.Height);
            var root = AutomationElement.FromHandle(w.Handle);

            // Bulk-fetch bounding rectangles in one cross-process request; None mode
            // returns lightweight cached stubs, which is all the hit test needs.
            var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
            cache.Add(AutomationElement.BoundingRectangleProperty);

            AutomationElementCollection found;
            using (cache.Activate())
            {
                found = root.FindAll(TreeScope.Descendants, Automation.ControlViewCondition);
            }

            int count = 0;
            foreach (AutomationElement el in found)
            {
                if (_cancelled || ++count > MaxElementsPerWindow) break;

                Rect r = el.Cached.BoundingRectangle;
                if (r.IsEmpty || r.Width < MinElementSize || r.Height < MinElementSize) continue;
                r.Intersect(winRect);
                if (r.IsEmpty || r.Width < MinElementSize || r.Height < MinElementSize) continue;
                list.Add(r);
            }
        }
        catch
        {
            // Elevated windows (UIPI) or broken UIA providers: window-level fallback.
        }
        return list;
    }
}
