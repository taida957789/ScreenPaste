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
    // 其他全域熱鍵（Snipaste 預設；留空即停用）：截圖並複製、自訂截圖、貼上剪貼簿／叫回關閉的貼圖、
    // 隱藏／顯示所有貼圖、切換游標下貼圖的滑鼠穿透（預設未指定）
    public string SnipAndCopyHotkey { get; set; } = "Ctrl+F1";
    public string CustomSnipHotkey { get; set; } = "Shift+F1";
    public string PasteHotkey { get; set; } = "F3";
    public string HidePinsHotkey { get; set; } = "Shift+F3";
    public string ClickThroughHotkey { get; set; } = "";
    // 更多截圖模式（預設皆未指定）
    public string SnipAndPinHotkey { get; set; } = "";
    public string SnipCopyAndPinHotkey { get; set; } = "";
    public string LastAreaCopyHotkey { get; set; } = "";
    public string FullScreenCopyHotkey { get; set; } = "";
    public string ActiveWindowHotkey { get; set; } = "";
    public string ActiveScreenHotkey { get; set; } = "";
    public string DelayedSnipHotkey { get; set; } = "";
    public string ClearSnipHistoryHotkey { get; set; } = "";
    // 延遲截圖與自訂截圖的等待秒數（0–60，自訂截圖對話框會記住上次的值）
    public double SnipDelaySeconds { get; set; } = 3;
    // 編輯器內快捷鍵（皆可自訂）
    public string UndoHotkey { get; set; } = "Ctrl+Z";
    public string RedoHotkey { get; set; } = "Ctrl+Y";
    public string CopyHotkey { get; set; } = "Ctrl+C";
    public string SaveHotkey { get; set; } = "Ctrl+S";
    public string QuickSaveHotkey { get; set; } = "Ctrl+Shift+S";
    public string PinHotkey { get; set; } = "Ctrl+T";
    public string PrintHotkey { get; set; } = "Ctrl+P";
    // 清除所有標註（可復原；留空即停用）
    public string ClearAllHotkey { get; set; } = "Ctrl+Shift+Z";

    // 框選完成後在範圍內按 Enter／雙擊／中鍵的動作：Copy | Pin | Save | QuickSave | CopyAndPin | None
    public string EnterAction { get; set; } = "Copy";
    public string DoubleClickAction { get; set; } = "Copy";
    public string MiddleClickAction { get; set; } = "Pin";

    // ---- Marker pen defaults ----
    public double PenWidth { get; set; } = 4;
    public string PenColor { get; set; } = "#FFFF3B30";   // opaque red
    public double PenOpacity { get; set; } = 1.0;

    // ---- Highlighter defaults ----
    public double HighlighterWidth { get; set; } = 18;
    public string HighlighterColor { get; set; } = "#FFFFEB3B"; // yellow
    public double HighlighterOpacity { get; set; } = 0.45;

    // ---- Blur defaults ----
    // 模糊類型：可於設定指定預設值；在截圖工具列切換後也會回存，所以下次沿用最後一次的選擇
    public string BlurKind { get; set; } = "Gaussian";   // Gaussian | Mosaic
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

    // ---- Number marker defaults ----
    // 序號標記：圓的直徑（顏色沿用形狀顏色）
    public double NumberSize { get; set; } = 28;

    // ---- Eraser defaults ----
    // 橡皮擦的大小（直徑）
    public double EraserWidth { get; set; } = 16;

    // ---- Magnifier ("local zoom") defaults ----
    // 局部放大：框出來源區域，旁邊以放大鏡顯示放大後的內容
    public string MagnifyShape { get; set; } = "RoundedRectangle";  // Rectangle | RoundedRectangle | Ellipse
    public double MagnifyZoom { get; set; } = 2.5;                  // 放大倍率
    public double MagnifyBorderWidth { get; set; } = 3;             // 邊框粗細（0 = 無邊框）
    public string MagnifyBorderColor { get; set; } = "#FFFF3B30";   // red
    public bool MagnifyConnector { get; set; } = true;              // 顯示與來源區域的連接線
    public bool MagnifyShadow { get; set; } = true;                 // 陰影
    public bool MagnifySmooth { get; set; } = true;                 // 平滑（關閉則顯示硬邊像素格）
    public bool MagnifyIncludeAnnotations { get; set; } = true;     // 放大內容是否含其他標註

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

    // ---- Behavior ----
    // Esc 離開編輯模式時，若有標註先跳確認對話框（可於對話框勾選「不再詢問」關閉）
    public bool ConfirmDiscardEdits { get; set; } = true;
    // 框選時顯示貫穿全螢幕的十字輔助線（Alt 單按切換，會記住上次的狀態）
    public bool SnipGuideLines { get; set; }
    // 截圖中顯示開始截圖時的滑鼠游標（按 ` 或 ! 切換，會記住上次的狀態）
    public bool SnipShowCursor { get; set; }
    // 截圖歷史（按 , / . 回放，只存在記憶體）：保留筆數 0–8（0 = 停用）與記憶體上限 MB（最多 512）
    public int SnipHistoryCount { get; set; } = 8;
    public int SnipHistoryBudgetMb { get; set; } = 512;
    // 貼圖滾輪縮放以滑鼠游標為中心（關閉則固定左上角）
    public bool PinZoomAtCursor { get; set; } = true;
    // 以 Esc／Ctrl+W／雙擊關閉的貼圖可再叫回的數量（0 = 停用；Shift+Esc 銷毀的不會保留）
    public int MaxRecoverableClosedPins { get; set; } = 1;
    // 雙擊貼圖：Close（關閉，可再叫回）| None（不動作）
    public string PinDoubleClickAction { get; set; } = "Close";
    // 第一次雙擊貼圖時已提示過
    public bool PinDoubleClickTipShown { get; set; }
    // 沒有貼圖取得焦點時，在任何地方按 Esc 會關閉所有顯示中的貼圖（可再叫回）
    public bool PinGlobalEscClosesAll { get; set; } = true;
    // Shift+雙擊貼圖切換的縮圖邊長（100% 縮放下的像素，16–512）
    public int PinThumbnailSize { get; set; } = 64;

    // ---- Startup ----
    public bool RunAtStartup { get; set; }

    // ---- Recording ----
    public string RecordFormat { get; set; } = "gif";   // gif | mp4 | webp
    public int RecordFps { get; set; } = 15;
    public bool RecordCaptureCursor { get; set; } = true;
    // none | system | mic | both — audio only lands in MP4 (GIF/WebP are silent).
    public string RecordAudioSource { get; set; } = "none";
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
