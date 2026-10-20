using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ScreenPaste.Native.NativeMethods;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;

namespace ScreenPaste.Native;

/// <summary>
/// The mouse pointer as it looked when a capture began (GDI CopyFromScreen leaves it out), so
/// ` / ! can show it in the snip: its image with real alpha, and where that image's top-left
/// sat in physical screen px. Taken before the overlay exists, whose own cursor would
/// otherwise be the one captured.
/// </summary>
public sealed record CursorSnapshot(BitmapSource Image, int ScreenX, int ScreenY)
{
    /// <summary>The image's top-left in overlay-local px.</summary>
    public Point LocalTo(VirtualScreen screen) => CursorPlacement.LocalTo(ScreenX, ScreenY, screen);

    /// <summary>The pointer now, or null when it is hidden (touch input, an app hiding it) or
    /// could not be drawn: the snip then simply has no pointer to show.</summary>
    public static CursorSnapshot? Capture()
    {
        if (Locate() is not { Width: > 0, Height: > 0 } shape) return null;
        try
        {
            var pixels = RecoverAlpha(Render(shape, Color.Black), Render(shape, Color.White));
            var image = BitmapSource.Create(shape.Width, shape.Height, 96, 96,
                PixelFormats.Pbgra32, null, pixels, shape.Width * 4);
            image.Freeze();
            return new CursorSnapshot(image, shape.Left, shape.Top);
        }
        // How GDI+ reports a failed draw (a cursor destroyed meanwhile, no desktop, a busy
        // device context). The capture goes on without a pointer rather than failing.
        catch (Exception ex) when (ex is ExternalException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The pointer as the screen shows it: its handle, and its image's top-left (hotspot
    /// applied) and size in physical screen px. Null while it is hidden. Shared with the
    /// recorder, which draws it straight into each frame (<c>Recording.CursorCapture</c>).
    /// </summary>
    internal static CursorShape? Locate()
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || ci.flags != CURSOR_SHOWING || ci.hCursor == IntPtr.Zero)
            return null;

        int hotX = 0, hotY = 0, width = 0, height = 0;
        if (GetIconInfo(ci.hCursor, out var ii))
        {
            hotX = ii.xHotspot;
            hotY = ii.yHotspot;
            (width, height) = ImageSize(ii);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
        }

        var (left, top) = CursorPlacement.TopLeft(ci.ptScreenPos.X, ci.ptScreenPos.Y, hotX, hotY);
        return new CursorShape(ci.hCursor, left, top, width, height);
    }

    /// <summary>A colour cursor is as big as its colour bitmap; a monochrome one keeps its AND
    /// and XOR masks stacked in one bitmap of twice its height.</summary>
    private static (int Width, int Height) ImageSize(ICONINFO ii)
    {
        int size = Marshal.SizeOf<BITMAP>();
        if (ii.hbmColor != IntPtr.Zero && GetObject(ii.hbmColor, size, out var color) != 0)
            return (color.bmWidth, color.bmHeight);
        if (ii.hbmMask != IntPtr.Zero && GetObject(ii.hbmMask, size, out var mask) != 0)
            return (mask.bmWidth, mask.bmHeight / 2);
        return (0, 0);
    }

    /// <summary>The cursor drawn over <paramref name="background"/>, as BGRA bytes (a GDI draw
    /// leaves the alpha byte meaningless, hence two backgrounds).</summary>
    private static byte[] Render(CursorShape shape, Color background)
    {
        using var bmp = new Bitmap(shape.Width, shape.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(background);
            IntPtr hdc = g.GetHdc();
            try { DrawIconEx(hdc, 0, 0, shape.Handle, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
            finally { g.ReleaseHdc(hdc); }
        }

        var data = bmp.LockBits(new Rectangle(0, 0, shape.Width, shape.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[shape.Width * shape.Height * 4];
            for (int y = 0; y < shape.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * shape.Width * 4, shape.Width * 4);
            return bytes;
        }
        finally { bmp.UnlockBits(data); }
    }

    /// <summary>
    /// Premultiplied BGRA from the same image drawn on black and on white. A pixel of colour c
    /// and alpha a gives c·a on black and c·a + 255·(1-a) on white, so the difference is the
    /// part of the background still showing. An inverting pixel (a monochrome cursor's XOR
    /// part) is lighter on black than on white and comes out opaque white.
    /// </summary>
    internal static byte[] RecoverAlpha(byte[] onBlack, byte[] onWhite)
    {
        var result = new byte[Math.Min(onBlack.Length, onWhite.Length) / 4 * 4];
        for (int i = 0; i < result.Length; i += 4)
        {
            int seen = 0;
            for (int c = 0; c < 3; c++) seen = Math.Max(seen, onWhite[i + c] - onBlack[i + c]);
            byte alpha = (byte)(255 - Math.Clamp(seen, 0, 255));
            for (int c = 0; c < 3; c++) result[i + c] = Math.Min(onBlack[i + c], alpha);
            result[i + 3] = alpha;
        }
        return result;
    }
}

/// <summary>The pointer's handle and where its image lands, in physical screen px.</summary>
internal readonly record struct CursorShape(IntPtr Handle, int Left, int Top, int Width, int Height);

/// <summary>Where the pointer's image goes: screen to overlay to area coordinates.</summary>
public static class CursorPlacement
{
    /// <summary>The image's top-left for a pointer at (<paramref name="pointerX"/>,
    /// <paramref name="pointerY"/>): the hotspot (the arrow's tip) sits on the pointer.</summary>
    public static (int X, int Y) TopLeft(int pointerX, int pointerY, int hotspotX, int hotspotY) =>
        (pointerX - hotspotX, pointerY - hotspotY);

    public static Point LocalTo(int screenX, int screenY, VirtualScreen screen) => screen.ToLocal(screenX, screenY);

    /// <summary>The image's rectangle in <paramref name="region"/>'s own coordinates, which may
    /// hang over its edges (drawing clips it), or null when none of it falls inside.
    /// <paramref name="topLeft"/> and <paramref name="region"/> share the screenshot's px.</summary>
    public static Rect? InRegion(Point topLeft, int width, int height, Int32Rect region)
    {
        if (width <= 0 || height <= 0) return null;
        var at = new Rect(topLeft.X - region.X, topLeft.Y - region.Y, width, height);
        bool overlaps = at.Right > 0 && at.Bottom > 0 && at.X < region.Width && at.Y < region.Height;
        return overlaps ? at : null;
    }
}
