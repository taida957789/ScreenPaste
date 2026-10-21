using System.Drawing;
using ScreenPaste.Native;
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
        // Offset by the cursor hotspot so the tip lands where the user sees it.
        if (CursorSnapshot.Locate() is not { } cursor) return;
        int x = cursor.Left - originX;
        int y = cursor.Top - originY;

        IntPtr hdc = g.GetHdc();
        try { DrawIconEx(hdc, x, y, cursor.Handle, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
        finally { g.ReleaseHdc(hdc); }
    }
}
