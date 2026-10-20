using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Output;

public static class ClipboardService
{
    private const string ClipPrefix = "clip_";
    private const string ClipExtension = ".png";
    private static readonly TimeSpan ClipMaxAge = TimeSpan.FromDays(1);

    /// <summary>Where copy-as-file writes its PNGs (shared with recording intermediates).</summary>
    public static string TempDirectory => Path.Combine(Path.GetTempPath(), "ScreenPaste");

    /// <summary>Copy an image to the clipboard, retrying briefly if it is locked.</summary>
    public static bool CopyImage(BitmapSource image) => Retry(() => Clipboard.SetImage(image));

    /// <summary>Copy plain text, retrying briefly if the clipboard is locked.</summary>
    public static bool CopyText(string text) => Retry(() => Clipboard.SetText(text));

    /// <summary>The clipboard's text ("" when it holds none), or null when it stayed locked.</summary>
    public static string? GetText()
    {
        string text = "";
        return Retry(() => text = Clipboard.GetText()) ? text : null;
    }

    /// <summary>
    /// Copy the image as a file (Snipaste's Ctrl+Alt+C), so it pastes into Explorer or an
    /// upload box: a PNG in <see cref="TempDirectory"/>, then put on the clipboard as a file
    /// drop. False when the file could not be written or the clipboard stayed locked.
    /// </summary>
    public static bool CopyAsFile(BitmapSource image)
    {
        string path;
        try
        {
            Directory.CreateDirectory(TempDirectory);
            path = Path.Combine(TempDirectory, ClipFileName(DateTime.Now));
            FileSaveService.Encode(image, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        return Retry(() => Clipboard.SetFileDropList(new StringCollection { path }));
    }

    /// <summary>App startup: delete the copy-as-file PNGs older than a day. Anything else in
    /// the folder (recording intermediates) is left alone. Best effort.</summary>
    public static void DeleteStaleClips()
    {
        try
        {
            var dir = new DirectoryInfo(TempDirectory);
            if (!dir.Exists) return;
            var now = DateTime.UtcNow;
            foreach (var file in dir.EnumerateFiles(ClipPrefix + "*" + ClipExtension))
            {
                if (!IsStaleClip(file.Name, file.LastWriteTimeUtc, now)) continue;
                try { file.Delete(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* in use: next startup */ }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp folder unreadable */ }
    }

    /// <summary>"clip_20261020_090507_042.png": local time to the millisecond, so two copies
    /// in the same second do not overwrite each other.</summary>
    internal static string ClipFileName(DateTime at) =>
        ClipPrefix + at.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ClipExtension;

    /// <summary>A copy-as-file PNG last written more than a day before <paramref name="nowUtc"/>.</summary>
    internal static bool IsStaleClip(string fileName, DateTime lastWriteUtc, DateTime nowUtc) =>
        fileName.StartsWith(ClipPrefix, StringComparison.OrdinalIgnoreCase) &&
        fileName.EndsWith(ClipExtension, StringComparison.OrdinalIgnoreCase) &&
        nowUtc - lastWriteUtc > ClipMaxAge;

    /// <summary>Another app holding the clipboard open fails the call with a COMException;
    /// it is usually released within a few milliseconds.</summary>
    private static bool Retry(Action write)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                write();
                return true;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(40);
            }
        }
        return false;
    }
}
