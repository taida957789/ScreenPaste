using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace ScreenPaste.Editor;

/// <summary>
/// Decodes an image file into a frozen WPF <see cref="BitmapSource"/>.
/// </summary>
/// <remarks>
/// WPF's own codecs (PNG, JPEG, BMP, GIF, TIFF — and WebP where the OS codec is installed)
/// are tried first, and SkiaSharp is the fallback that adds WebP everywhere else. The order
/// matters for robustness, not speed: SkiaSharp needs the native libSkiaSharp, which in the
/// single-file build is extracted under %TEMP% and can go missing while the app sits in the
/// tray (see the note in <see cref="ScreenPaste.Settings.FlagIcons"/>, and issue #2). Putting
/// the managed codecs first means the common formats keep working even then, and only WebP
/// degrades.
/// </remarks>
public static class ImageLoader
{
    public const string FileFilter =
        "圖片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有檔案 (*.*)|*.*";

    public static BitmapSource? TryLoad(string path) => LoadWithWpf(path) ?? LoadWithSkia(path);

    /// <summary>WPF's built-in WIC codecs. Returns null for a format it does not know.</summary>
    private static BitmapSource? LoadWithWpf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            // OnLoad pulls the whole image into memory so the file handle is released here,
            // rather than being held for as long as the sticker lives.
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;

            var frame = decoder.Frames[0];
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
        catch { return null; }
    }

    /// <summary>SkiaSharp, for anything WPF could not decode (in practice: WebP).</summary>
    private static BitmapSource? LoadWithSkia(string path)
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
