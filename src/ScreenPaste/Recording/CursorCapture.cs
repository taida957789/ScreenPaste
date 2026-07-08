using System.Drawing;
using System.Runtime.InteropServices;
using static ScreenPaste.Native.NativeMethods;

namespace ScreenPaste.Recording;

/// <summary>Draws the current mouse pointer into a captured frame (GDI CopyFromScreen omits it).</summary>
public static class CursorCapture
{
    /// <summary>
    /// Draw the pointer onto <paramref name="g"/>, whose origin corresponds to the physical
    /// screen point (<paramref name="originX"/>, <paramref name="originY"/>).
    /// </summary>
    public static void Draw(Graphics g, int originX, int originY)
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || ci.flags != CURSOR_SHOWING || ci.hCursor == IntPtr.Zero)
            return;

        int x = ci.ptScreenPos.X - originX;
        int y = ci.ptScreenPos.Y - originY;

        // Offset by the cursor hotspot so the tip lands where the user sees it.
        if (GetIconInfo(ci.hCursor, out var ii))
        {
            x -= ii.xHotspot;
            y -= ii.yHotspot;
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
        }

        IntPtr hdc = g.GetHdc();
        try { DrawIconEx(hdc, x, y, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
        finally { g.ReleaseHdc(hdc); }
    }
}
