using System.IO;

namespace ScreenPaste.Recording;

/// <summary>Finds the bundled (or system) ffmpeg executable.</summary>
public static class FFmpegLocator
{
    /// <summary>
    /// Returns the path/command to invoke ffmpeg, or null if no bundled copy is found.
    /// Prefers a copy shipped next to the app; falls back to "ffmpeg" on PATH.
    /// </summary>
    public static string? Find()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        {
            Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Last resort: rely on PATH. Process.Start throws if it is not installed,
        // which the recorder surfaces to the user.
        return OnPath() ? "ffmpeg" : null;
    }

    private static bool OnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return false;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(dir) &&
                    File.Exists(Path.Combine(dir.Trim(), "ffmpeg.exe")))
                    return true;
            }
            catch { /* ignore malformed PATH entries */ }
        }
        return false;
    }
}
