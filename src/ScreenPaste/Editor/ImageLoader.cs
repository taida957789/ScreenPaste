using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace ScreenPaste.Editor;

/// <summary>Decodes PNG / JPEG / WebP (via SkiaSharp) into a frozen WPF BitmapSource.</summary>
public static class ImageLoader
{
    public const string FileFilter =
        "圖片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有檔案 (*.*)|*.*";

    public static BitmapSource? TryLoad(string path)
    {
        try
        {
            using var decoded = SKBitmap.Decode(path);
            if (decoded == null) return null;

            using var bmp = decoded.Copy(SKColorType.Bgra8888);
            if (bmp == null) return null;

            // Match the WPF format to Skia's alpha type (premultiplied vs straight).
            var fmt = bmp.AlphaType == SKAlphaType.Premul ? PixelFormats.Pbgra32 : PixelFormats.Bgra32;

            var bs = BitmapSource.Create(bmp.Width, bmp.Height, 96, 96,
                fmt, null, bmp.Bytes, bmp.RowBytes);
            bs.Freeze();
            return bs;
        }
        catch { return null; }
    }
}
