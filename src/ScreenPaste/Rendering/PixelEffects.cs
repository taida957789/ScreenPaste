using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Rendering;

/// <summary>
/// A pin's colour effects (Snipaste's 5 and 6): grayscale and invert on BGRA bytes. The
/// pixels are premultiplied (Pbgra32: each colour channel at most its alpha), which both
/// effects keep valid without unpremultiplying: grayscale is a weighted sum, and invert is
/// c' = a - c. Alpha never changes. The byte versions are pure and return new arrays, so
/// they are unit tested directly.
/// </summary>
public static class PixelEffects
{
    // Rec. 601 luma in 1/256ths (R 0.299, G 0.587, B 0.114); they sum to 256, so white stays white.
    private const int RedWeight = 77;
    private const int GreenWeight = 150;
    private const int BlueWeight = 29;

    /// <summary>Each pixel's luma in all three colour channels.</summary>
    public static byte[] Grayscale(byte[] bgra)
    {
        CheckWholePixels(bgra);
        var result = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            byte luma = (byte)((bgra[i + 2] * RedWeight + bgra[i + 1] * GreenWeight + bgra[i] * BlueWeight + 128) >> 8);
            result[i] = luma;
            result[i + 1] = luma;
            result[i + 2] = luma;
            result[i + 3] = bgra[i + 3];
        }
        return result;
    }

    /// <summary>Each colour channel turned into its negative within the pixel's alpha.</summary>
    public static byte[] Invert(byte[] pbgra)
    {
        CheckWholePixels(pbgra);
        var result = new byte[pbgra.Length];
        for (int i = 0; i < pbgra.Length; i += 4)
        {
            byte a = pbgra[i + 3];
            result[i] = Negative(pbgra[i], a);
            result[i + 1] = Negative(pbgra[i + 1], a);
            result[i + 2] = Negative(pbgra[i + 2], a);
            result[i + 3] = a;
        }
        return result;
    }

    /// <summary>
    /// <paramref name="source"/> with the chosen effects (grayscale first, then invert), as a
    /// frozen Pbgra32 bitmap at the source's size and DPI; the source itself when neither is on.
    /// </summary>
    public static BitmapSource Apply(BitmapSource source, bool grayscale, bool invert)
    {
        if (!grayscale && !invert) return source;

        BitmapSource premultiplied = source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        int w = premultiplied.PixelWidth, h = premultiplied.PixelHeight, stride = w * 4;
        var pixels = new byte[stride * h];
        premultiplied.CopyPixels(pixels, stride, 0);

        if (grayscale) pixels = Grayscale(pixels);
        if (invert) pixels = Invert(pixels);

        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY, PixelFormats.Pbgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    // A channel above its alpha (not valid premultiplied data) clamps to black.
    private static byte Negative(byte channel, byte alpha) => channel >= alpha ? (byte)0 : (byte)(alpha - channel);

    private static void CheckWholePixels(byte[] pixels)
    {
        if (pixels.Length % 4 != 0)
            throw new ArgumentException("BGRA data must be whole 4-byte pixels.", nameof(pixels));
    }
}
