using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;

namespace ScreenPaste.Output;

/// <summary>
/// Turns what the clipboard holds into a pin's content (the paste hotkey, the tray, a pin's
/// Ctrl+V): an image (a PNG stream or a DIB keeps its alpha), an image file copied in
/// Explorer or named by a copied path, a colour value as a colour card, or any other text
/// drawn as a text pin that remembers its text. <see cref="ClipboardFormatPicker"/> decides
/// the order; a source that fails to load (another app's data gone, a broken file) gives
/// way to the next.
/// </summary>
public static partial class ClipboardPinSource
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;           // BITMAPINFOHEADER; V4 and V5 carry their masks inside
    private const int MaxHeaderSize = 124;           // BITMAPV5HEADER
    private const uint BiBitfields = 3;
    private const uint BiAlphaBitfields = 6;
    private const int OpenAttempts = 5;
    private const string FragmentStart = "<!--StartFragment-->";
    private const string FragmentEnd = "<!--EndFragment-->";
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(40);

    /// <summary>What the clipboard holds as a pin drawn for a monitor of <paramref name="scale"/>
    /// (physical px per DIP), or null: nothing a pin can show, or the clipboard stayed locked.</summary>
    public static PinContent? FromClipboard(double scale) =>
        OpenClipboard() is { } data ? Resolve(data, scale) : null;

    public static PinContent? Resolve(IDataObject data, double scale)
    {
        var formats = Try(() => data.GetFormats()) ?? Array.Empty<string>();
        var files = formats.Contains(ClipboardFormatPicker.FileDropFormat, StringComparer.OrdinalIgnoreCase)
            ? Try(() => data.GetData(DataFormats.FileDrop) as string[])
            : null;
        foreach (var source in ClipboardFormatPicker.Pick(formats, files))
        {
            if (Load(data, source, files, scale) is { } content) return content;
        }
        return null;
    }

    private static PinContent? Load(IDataObject data, ClipboardSource source, string[]? files, double scale)
    {
        switch (source.Kind)
        {
            case ClipboardSourceKind.Png: return AsImage(LoadPng(data));
            case ClipboardSourceKind.DibV5: return AsImage(Bytes(data, ClipboardFormatPicker.DibV5Format) is { } v5 ? DecodeDib(v5) : null);
            case ClipboardSourceKind.Dib: return AsImage(Bytes(data, DataFormats.Dib) is { } dib ? DecodeDib(dib) : null);
            case ClipboardSourceKind.Bitmap: return AsImage(LoadBitmap(data));
            case ClipboardSourceKind.ImageFile: return AsImage(ImageLoader.TryLoad(source.FileName!));
            case ClipboardSourceKind.Text: return FromText(Try(() => data.GetData(DataFormats.UnicodeText) as string ?? data.GetData(DataFormats.Text) as string), scale);
            case ClipboardSourceKind.Html: return FromText(Try(() => data.GetData(DataFormats.Html) as string) is { } html ? HtmlToText(html) : null, scale);
            case ClipboardSourceKind.FileList: return FromText(files is { Length: > 0 } ? string.Join(Environment.NewLine, files) : null, scale);
            default: return null;
        }
    }

    private static PinContent? AsImage(BitmapSource? image) => image == null ? null : new PinContent(image);

    /// <summary>Text as a pin: a path to an image file pins the image, a colour value a colour
    /// card, anything else the text itself. The pin keeps the text either way but the image.</summary>
    private static PinContent? FromText(string? text, double scale)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (ClipboardFormatPicker.ImagePathIn(text) is { } path && ImageLoader.TryLoad(path) is { } image)
            return new PinContent(image);
        if (ColorText.TryParse(text, out var color))
            return new PinContent(TextPinRenderer.RenderColor(color, scale), text.Trim());
        return new PinContent(TextPinRenderer.RenderText(text, scale), text);
    }

    // ------------------------------------------------------------- images ---

    private static BitmapSource? LoadPng(IDataObject data)
    {
        var stream = Try(() => data.GetData(ClipboardFormatPicker.PngFormat) as Stream ?? data.GetData(ClipboardFormatPicker.PngMimeFormat) as Stream);
        if (stream == null) return null;
        using (stream) return Decode(() => BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));
    }

    /// <summary>CF_DIB or CF_DIBV5 as an image, through a BMP file WIC decodes. A DIBV5 whose
    /// alpha is zero everywhere (Windows makes one from a plain bitmap) is shown opaque, not invisible.</summary>
    internal static BitmapSource? DecodeDib(byte[] dib)
    {
        if (BmpFileFromDib(dib) is not { } file) return null;
        var image = Decode(() => new BmpBitmapDecoder(new MemoryStream(file), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));
        if (image == null || !HasAlpha(image.Format)) return image;

        var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        if (!IsFullyTransparent(pixels)) return image;
        var opaque = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
        opaque.Freeze();
        return opaque;
    }

    /// <summary>CF_BITMAP through WPF's own conversion, copied so the clipboard's handle can go.</summary>
    private static BitmapSource? LoadBitmap(IDataObject data)
    {
        if (Try(() => data.GetData(DataFormats.Bitmap) as BitmapSource) is not { } source) return null;
        try
        {
            var copy = new WriteableBitmap(source);
            copy.Freeze();
            return copy;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static BitmapSource? Decode(Func<BitmapDecoder> create)
    {
        try
        {
            var decoder = create();
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
        // A malformed or unsupported image: WIC reports it in several ways.
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or COMException or
                                   ArgumentException or IOException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    private static bool HasAlpha(PixelFormat format) =>
        format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 || format == PixelFormats.Prgba64 ||
        format == PixelFormats.Rgba64 || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float;

    /// <summary>
    /// A BMP file around a packed DIB (what CF_DIB and CF_DIBV5 hold): the 14-byte file
    /// header, whose pixel offset points past the info header, the bit masks that follow a
    /// plain BITMAPINFOHEADER, and the colour table. Null when the header does not add up.
    /// </summary>
    internal static byte[]? BmpFileFromDib(byte[] dib)
    {
        if (dib.Length < InfoHeaderSize) return null;
        int headerSize = BitConverter.ToInt32(dib, 0);
        if (headerSize < InfoHeaderSize || headerSize > MaxHeaderSize || headerSize > dib.Length) return null;

        int bitCount = BitConverter.ToUInt16(dib, 14);
        uint compression = BitConverter.ToUInt32(dib, 16);
        uint colorsUsed = BitConverter.ToUInt32(dib, 32);
        int masks = headerSize != InfoHeaderSize ? 0
            : compression == BiBitfields ? 12
            : compression == BiAlphaBitfields ? 16
            : 0;
        long colors = colorsUsed != 0 ? colorsUsed : bitCount is > 0 and <= 8 ? 1L << bitCount : 0;
        long pixelStart = (long)headerSize + masks + colors * 4;
        if (pixelStart > dib.Length) return null;

        var file = new byte[FileHeaderSize + dib.Length];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BitConverter.GetBytes(file.Length).CopyTo(file, 2);
        BitConverter.GetBytes((int)(FileHeaderSize + pixelStart)).CopyTo(file, 10);
        dib.CopyTo(file, FileHeaderSize);
        return file;
    }

    /// <summary>Whether every pixel of BGRA <paramref name="bgra"/> has zero alpha (and there is one).</summary>
    internal static bool IsFullyTransparent(byte[] bgra)
    {
        if (bgra.Length < 4) return false;
        for (int i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0) return false;
        }
        return true;
    }

    // --------------------------------------------------------------- text ---

    /// <summary>CF_HTML as plain text: the copied fragment, with line breaks where blocks end
    /// and no tags, scripts or styles.</summary>
    internal static string HtmlToText(string cfHtml)
    {
        var html = cfHtml;
        int start = html.IndexOf(FragmentStart, StringComparison.OrdinalIgnoreCase);
        int end = html.IndexOf(FragmentEnd, StringComparison.OrdinalIgnoreCase);
        if (start >= 0 && end > start) html = html[(start + FragmentStart.Length)..end];
        else if (html.StartsWith("Version:", StringComparison.OrdinalIgnoreCase) && html.IndexOf('<') is var tag and >= 0)
            html = html[tag..];   // no fragment marks: at least skip the CF_HTML header

        html = HiddenPattern().Replace(html, "");
        html = WhitespacePattern().Replace(html, " ");   // as a browser collapses it
        html = LineBreakPattern().Replace(html, "\n");
        html = TagPattern().Replace(html, "");
        var text = WebUtility.HtmlDecode(html).Replace("\r\n", "\n");
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenPattern();

    [GeneratedRegex(@"<(br\b[^>]*|/(p|div|li|tr|h[1-6]|table|ul|ol)\s*)>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagPattern();

    // ---------------------------------------------------------- clipboard ---

    /// <summary>The clipboard's data, retrying briefly while another app holds it open; null
    /// when it stayed locked.</summary>
    private static IDataObject? OpenClipboard()
    {
        for (int attempt = 0; attempt < OpenAttempts; attempt++)
        {
            try { return Clipboard.GetDataObject(); }
            catch (COMException) { Thread.Sleep(OpenRetryDelay); }
        }
        return null;
    }

    private static byte[]? Bytes(IDataObject data, string format)
    {
        if (Try(() => data.GetData(format) as Stream) is not { } stream) return null;
        using (stream)
        {
            try
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            catch (IOException) { return null; }
        }
    }

    /// <summary>Another app's clipboard data can be gone, or refuse a format, by the time it is read.</summary>
    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
