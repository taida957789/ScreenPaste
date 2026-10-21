using System.IO;

namespace ScreenPaste.Output;

/// <summary>A kind of clipboard content a pin can be made from.</summary>
public enum ClipboardSourceKind
{
    Png,        // a PNG stream ("PNG", "image/png"): browsers, Office, other snipping tools; keeps alpha
    DibV5,      // CF_DIBV5 ("Format17"): may carry alpha
    Dib,        // CF_DIB
    Bitmap,     // CF_BITMAP, through WPF's own conversion
    ImageFile,  // a copied file that looks like an image
    Text,       // plain text: a colour value, a path to an image, or text to show
    Html,       // HTML with no plain text alongside, shown as its text
    FileList,   // copied files that are not images: their names, as text
}

/// <summary>One thing to try; <see cref="FileName"/> is set for <see cref="ClipboardSourceKind.ImageFile"/>.</summary>
public readonly record struct ClipboardSource(ClipboardSourceKind Kind, string? FileName = null);

/// <summary>
/// Which clipboard content the paste hotkey tries, best first, from the format names the
/// clipboard offers (and the copied files' names): the images, which keep their pixels as
/// they are, then image files, then text. ClipboardPinSource tries each until one loads.
/// Pure, so the order is unit tested.
/// </summary>
public static class ClipboardFormatPicker
{
    public const string PngFormat = "PNG";
    public const string PngMimeFormat = "image/png";
    public const string DibV5Format = "Format17";   // CF_DIBV5 has no registered name; WPF calls it this
    public const string DibFormat = "DeviceIndependentBitmap";
    public const string BitmapFormat = "Bitmap";
    public const string FileDropFormat = "FileDrop";
    public const string HtmlFormat = "HTML Format";
    private static readonly string[] TextFormats = { "UnicodeText", "Text", "OEMText", "System.String" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico",
    };

    public static IReadOnlyList<ClipboardSource> Pick(IEnumerable<string> formatNames, IReadOnlyList<string>? fileNames)
    {
        var formats = new HashSet<string>(formatNames, StringComparer.OrdinalIgnoreCase);
        var picks = new List<ClipboardSource>();
        if (formats.Contains(PngFormat) || formats.Contains(PngMimeFormat)) picks.Add(new(ClipboardSourceKind.Png));
        if (formats.Contains(DibV5Format)) picks.Add(new(ClipboardSourceKind.DibV5));
        if (formats.Contains(DibFormat)) picks.Add(new(ClipboardSourceKind.Dib));
        if (formats.Contains(BitmapFormat)) picks.Add(new(ClipboardSourceKind.Bitmap));

        var files = formats.Contains(FileDropFormat) ? fileNames ?? Array.Empty<string>() : Array.Empty<string>();
        foreach (var file in files.Where(IsImageFile)) picks.Add(new(ClipboardSourceKind.ImageFile, file));

        if (TextFormats.Any(formats.Contains)) picks.Add(new(ClipboardSourceKind.Text));
        if (formats.Contains(HtmlFormat)) picks.Add(new(ClipboardSourceKind.Html));
        if (files.Count > 0) picks.Add(new(ClipboardSourceKind.FileList));
        return picks;
    }

    /// <summary>The image file <paramref name="text"/> names when it is one full path to one
    /// (quotes around it allowed, as Explorer's "Copy as path" writes it), else null.</summary>
    public static string? ImagePathIn(string text)
    {
        var path = text.Trim().Trim('"').Trim();
        if (path.Length == 0 || path.Contains('\n') || path.Contains('\r')) return null;
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(path)) return null;
        return IsImageFile(path) ? path : null;
    }

    private static bool IsImageFile(string fileName) => ImageExtensions.Contains(Path.GetExtension(fileName));
}
