namespace ScreenPaste.Recording;

/// <summary>Output container/codec for a screen recording.</summary>
public enum RecordingFormat
{
    Gif,
    Mp4,
    WebP,
}

public static class RecordingFormats
{
    /// <summary>Parse a settings string ("gif" | "mp4" | "webp"); defaults to GIF.</summary>
    public static RecordingFormat Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "mp4" => RecordingFormat.Mp4,
        "webp" => RecordingFormat.WebP,
        _ => RecordingFormat.Gif,
    };

    /// <summary>File extension including the leading dot.</summary>
    public static string Extension(this RecordingFormat f) => f switch
    {
        RecordingFormat.Mp4 => ".mp4",
        RecordingFormat.WebP => ".webp",
        _ => ".gif",
    };

    /// <summary>Lower-case settings token.</summary>
    public static string Token(this RecordingFormat f) => f switch
    {
        RecordingFormat.Mp4 => "mp4",
        RecordingFormat.WebP => "webp",
        _ => "gif",
    };
}
