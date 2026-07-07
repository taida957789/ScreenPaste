using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ScreenPaste.Output;

public static class FileSaveService
{
    /// <summary>Prompt for a path and save as PNG or JPEG. Returns the path, or null if cancelled.</summary>
    public static string? SaveAs(BitmapSource image, string defaultDirectory)
    {
        Directory.CreateDirectory(defaultDirectory);
        var dlg = new SaveFileDialog
        {
            InitialDirectory = defaultDirectory,
            FileName = SuggestName(),
            Filter = "PNG 影像 (*.png)|*.png|JPEG 影像 (*.jpg)|*.jpg",
            DefaultExt = ".png",
            AddExtension = true,
        };
        if (dlg.ShowDialog() != true) return null;

        Encode(image, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>Save straight into the default folder with a timestamped name.</summary>
    public static string QuickSave(BitmapSource image, string defaultDirectory)
    {
        Directory.CreateDirectory(defaultDirectory);
        var path = Path.Combine(defaultDirectory, SuggestName() + ".png");
        Encode(image, path);
        return path;
    }

    private static string SuggestName() => "ScreenPaste_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

    private static void Encode(BitmapSource image, string path)
    {
        BitmapEncoder encoder = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
            _ => new PngBitmapEncoder(),
        };
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }
}
