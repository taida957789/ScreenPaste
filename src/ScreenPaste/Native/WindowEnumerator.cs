using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using static ScreenPaste.Native.NativeMethods;

namespace ScreenPaste.Native;

/// <summary>A candidate window with its true visible bounds in physical px.</summary>
internal readonly record struct DetectedWindow(IntPtr Handle, string Title, RECT Bounds);

/// <summary>
/// Enumerates visible top-level windows (front-to-back Z-order) and hit-tests the
/// cursor against them, so the overlay can auto-outline the window under the mouse.
/// </summary>
internal static class WindowEnumerator
{
    /// <summary>Snapshot of candidate windows, ordered topmost-first.</summary>
    public static List<DetectedWindow> Enumerate(IntPtr selfHandle)
    {
        var result = new List<DetectedWindow>();

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == selfHandle) return true;              // never detect our own overlay
            if (!IsWindowVisible(hWnd)) return true;
            if (IsIconic(hWnd)) return true;                  // minimized

            long style = (uint)GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_VISIBLE) == 0) return true;

            long ex = (uint)GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) != 0) return true;    // skip tool windows

            // Skip DWM-cloaked windows (e.g. background UWP apps that report "visible").
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            RECT bounds = GetVisibleBounds(hWnd);
            if (bounds.Width <= 0 || bounds.Height <= 0) return true;

            result.Add(new DetectedWindow(hWnd, GetTitle(hWnd), bounds));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>Hit-test a physical-px point; returns the topmost window's local rect.</summary>
    public static Rect? HitTest(IReadOnlyList<DetectedWindow> windows, VirtualScreen vs, int screenX, int screenY)
    {
        return HitTestWindow(windows, screenX, screenY) is { } w
            ? new Rect(w.Bounds.Left - vs.X, w.Bounds.Top - vs.Y, w.Bounds.Width, w.Bounds.Height)
            : null;
    }

    /// <summary>Hit-test a physical-px point; returns the topmost window itself.</summary>
    public static DetectedWindow? HitTestWindow(IReadOnlyList<DetectedWindow> windows, int screenX, int screenY)
    {
        foreach (var w in windows)
        {
            var b = w.Bounds;
            if (screenX >= b.Left && screenX < b.Right && screenY >= b.Top && screenY < b.Bottom)
                return w;
        }
        return null;
    }

    /// <summary>Prefer DWM extended frame bounds (no shadow); fall back to GetWindowRect.</summary>
    private static RECT GetVisibleBounds(IntPtr hWnd)
    {
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r,
                Marshal.SizeOf<RECT>()) == 0)
        {
            return r;
        }
        GetWindowRect(hWnd, out RECT fallback);
        return fallback;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
