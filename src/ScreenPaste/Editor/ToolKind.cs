namespace ScreenPaste.Editor;

public enum ToolKind
{
    None,
    MarkerPen,     // 麥克筆：實心筆觸
    Highlighter,   // 螢光筆：半透明疊色
    Text,          // 文字標註
    Shape,         // 形狀：方形 / 圓角方形 / 圓形，外框或填滿
    Sticker,       // 貼圖：貼上 PNG / JPEG / WebP 圖片
    Blur,          // 模糊：於選項列再選高斯 / 馬賽克
}

/// <summary>形狀子類型。</summary>
public enum ShapeKind
{
    Rectangle,        // 方形
    RoundedRectangle, // 圓角方形
    Ellipse,          // 圓形 / 橢圓
}

/// <summary>模糊子類型，於「模糊」工具的選項列切換。</summary>
public enum BlurKind
{
    Gaussian,      // 高斯模糊
    Mosaic,        // 馬賽克
}
