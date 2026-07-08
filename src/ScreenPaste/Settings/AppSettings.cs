using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenPaste.Settings;

/// <summary>User preferences, persisted as JSON under %AppData%\ScreenPaste\settings.json.</summary>
public sealed class AppSettings
{
    // ---- Hotkeys (human-friendly gesture strings, e.g. "F1", "Ctrl+Shift+A", "Ctrl+Z") ----
    // 全域截圖熱鍵（系統層級）
    public string CaptureHotkey { get; set; } = "F1";
    // 全域區域錄影熱鍵（再按一次停止）
    public string RecordHotkey { get; set; } = "F2";
    // 編輯器內快捷鍵（皆可自訂）
    public string UndoHotkey { get; set; } = "Ctrl+Z";
    public string RedoHotkey { get; set; } = "Ctrl+Y";
    public string CopyHotkey { get; set; } = "Ctrl+C";
    public string SaveHotkey { get; set; } = "Ctrl+S";
    public string QuickSaveHotkey { get; set; } = "Ctrl+Shift+S";

    // ---- Marker pen defaults ----
    public double PenWidth { get; set; } = 4;
    public string PenColor { get; set; } = "#FFFF3B30";   // opaque red
    public double PenOpacity { get; set; } = 1.0;

    // ---- Highlighter defaults ----
    public double HighlighterWidth { get; set; } = 18;
    public string HighlighterColor { get; set; } = "#FFFFEB3B"; // yellow
    public double HighlighterOpacity { get; set; } = 0.45;

    // ---- Blur defaults ----
    public double GaussianStrength { get; set; } = 12;   // BlurEffect.Radius
    public double MosaicStrength { get; set; } = 12;     // block size in px

    // ---- Text defaults ----
    public string TextFont { get; set; } = "Segoe UI";
    public double TextSize { get; set; } = 24;
    public string TextColor { get; set; } = "#FFFF3B30"; // red
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public bool TextStrikethrough { get; set; }

    // ---- Shape defaults ----
    public string ShapeKind { get; set; } = "Rectangle";  // Rectangle | RoundedRectangle | Ellipse
    public bool ShapeFilled { get; set; }
    public string ShapeColor { get; set; } = "#FFFF3B30"; // red
    public double ShapeWidth { get; set; } = 3;

    // ---- Custom colour swatches added via the colour picker (hex, oldest first) ----
    public List<string> CustomColors { get; set; } = new();

    // ---- Line defaults (each end can be an arrowhead) ----
    public double LineWidth { get; set; } = 3;
    public string LineColor { get; set; } = "#FFFF3B30"; // red
    public bool LineArrowStart { get; set; }
    public bool LineArrowEnd { get; set; } = true;

    // ---- Appearance ----
    public string Theme { get; set; } = "System";   // System | Light | Dark
    public string Language { get; set; } = "System"; // System | zh-Hant | en | ja | ko | fr | de | es

    // ---- Startup ----
    public bool RunAtStartup { get; set; }

    // ---- Recording ----
    public string RecordFormat { get; set; } = "gif";   // gif | mp4 | webp
    public int RecordFps { get; set; } = 15;
    public bool RecordCaptureCursor { get; set; } = true;
    // true = 錄完直接輸出檔案（跳過修剪/匯出編輯器）
    public bool RecordSkipEditor { get; set; }

    // ---- Updates ----
    public bool CheckUpdateOnStartup { get; set; } = true;

    // ---- Output ----
    public string SaveDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenPaste");

    [JsonIgnore]
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenPaste");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null) return s;
            }
        }
        catch { /* fall back to defaults on any corruption */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* non-fatal */ }
    }
}
