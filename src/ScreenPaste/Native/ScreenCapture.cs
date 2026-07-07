using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Size = System.Drawing.Size;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ScreenPaste.Native;

/// <summary>Bounds of the whole virtual desktop, in physical pixels.</summary>
public readonly record struct VirtualScreen(int X, int Y, int Width, int Height)
{
    public static VirtualScreen Current => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    /// <summary>Map a physical screen point into overlay-local (0,0)-origin px space.</summary>
    public Point ToLocal(int screenX, int screenY) => new(screenX - X, screenY - Y);
}

public static class ScreenCapture
{
    /// <summary>
    /// Grab the entire virtual desktop as a frozen bitmap in physical pixels.
    /// The returned BitmapSource is frozen (safe to hand across threads).
    /// </summary>
    public static BitmapSource CaptureVirtualScreen(out VirtualScreen bounds)
    {
        bounds = VirtualScreen.Current;
        int w = Math.Max(1, bounds.Width);
        int h = Math.Max(1, bounds.Height);

        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new Size(w, h),
                CopyPixelOperation.SourceCopy);
        }
        return ToBitmapSource(bmp);
    }

    /// <summary>Copy a GDI+ bitmap into a frozen WPF BitmapSource (no HBITMAP lifetime traps).</summary>
    private static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            int stride = data.Stride;
            int size = stride * bmp.Height;
            var buffer = new byte[size];
            Marshal.Copy(data.Scan0, buffer, 0, size);

            var bs = BitmapSource.Create(bmp.Width, bmp.Height, 96, 96,
                PixelFormats.Pbgra32, null, buffer, stride);
            bs.Freeze();
            return bs;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
