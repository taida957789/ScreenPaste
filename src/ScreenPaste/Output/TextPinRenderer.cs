using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>
/// Draws what the paste hotkey pins from text: the text itself on a panel in the theme's
/// colours, wrapped at 800 DIPs (Snipaste's text pin), or a colour card for a colour value.
/// Drawn in physical px at the scale of the monitor the pin opens on, so the text has its
/// usual size there. Very long text is cut short with an ellipsis; the pin keeps the whole
/// text for Ctrl+Shift+C.
/// </summary>
public static class TextPinRenderer
{
    public const double MaxWidthDip = 800;
    public const double MaxHeightDip = 2000;
    public const double PaddingDip = 10;
    private const double FontSizeDip = 14;
    private const int MaxChars = 20_000;              // more than fits the height; bounds the layout work
    private const double SwatchWidthDip = 160;
    private const double SwatchHeightDip = 96;
    private const double LabelHeightDip = 44;
    private const double LabelFontSizeDip = 13;

    /// <summary>The text as a pin's image, <paramref name="scale"/> physical px per DIP.</summary>
    public static BitmapSource RenderText(string text, double scale)
    {
        scale = SafeScale(scale);
        var shown = text.Length > MaxChars ? text[..MaxChars] : text;
        var formatted = Formatted(shown, FontSizeDip * scale, Theme.ForegroundBrush);
        double pad = PaddingDip * scale;
        formatted.MaxTextWidth = (MaxWidthDip - 2 * PaddingDip) * scale;
        formatted.MaxTextHeight = MaxHeightDip * scale;
        formatted.Trimming = TextTrimming.CharacterEllipsis;

        int width = (int)Math.Ceiling(formatted.WidthIncludingTrailingWhitespace + 2 * pad);
        int height = (int)Math.Ceiling(formatted.Height + 2 * pad);
        width = Math.Clamp(width, 1, (int)Math.Ceiling(MaxWidthDip * scale));
        return Render(width, Math.Max(1, height), dc =>
        {
            dc.DrawRectangle(Theme.WindowBrush, null, new Rect(0, 0, width, height));
            dc.DrawText(formatted, new Point(pad, pad));
        });
    }

    /// <summary>A colour value as a card: a swatch of the colour over its HEX and RGB values.</summary>
    public static BitmapSource RenderColor(Color color, double scale)
    {
        scale = SafeScale(scale);
        int width = (int)Math.Round(SwatchWidthDip * scale);
        int swatch = (int)Math.Round(SwatchHeightDip * scale);
        int height = swatch + (int)Math.Round(LabelHeightDip * scale);
        var hex = color.A == byte.MaxValue
            ? ColorFormat.Format(color, ColorFormatKind.Hex)
            : FormattableString.Invariant($"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        var label = Formatted(hex + "\n" + ColorFormat.Format(color, ColorFormatKind.Rgb),
            LabelFontSizeDip * scale, Theme.ForegroundBrush);
        label.TextAlignment = TextAlignment.Center;
        label.MaxTextWidth = width;

        return Render(width, height, dc =>
        {
            dc.DrawRectangle(Theme.WindowBrush, null, new Rect(0, 0, width, height));
            var fill = new SolidColorBrush(color);
            fill.Freeze();
            dc.DrawRectangle(fill, null, new Rect(0, 0, width, swatch));
            dc.DrawText(label, new Point(0, swatch + (height - swatch - label.Height) / 2));
        });
    }

    private static FormattedText Formatted(string text, double emSize, Brush brush) => new(
        text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        emSize, brush, pixelsPerDip: 1.0);   // drawn in physical px at 96 dpi: one unit is one pixel

    private static BitmapSource Render(int width, int height, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) draw(dc);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static double SafeScale(double scale) => double.IsFinite(scale) && scale > 0 ? scale : 1.0;
}
