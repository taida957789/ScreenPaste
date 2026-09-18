using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Settings;

/// <summary>
/// Small simplified flag icons for the language combo, drawn with WPF.
/// </summary>
/// <remarks>
/// Deliberately NOT SkiaSharp. These are a handful of bars and circles, but drawing them
/// with Skia made opening Settings depend on the native libSkiaSharp being loadable. In the
/// single-file build that native lives in a %TEMP% extraction folder, and since the app sits
/// in the tray for days while Skia is only ever touched the first time Settings is opened,
/// anything that cleans %TEMP% in between (disk cleanup, antivirus) leaves the app running
/// with the native gone — and the window died with
/// <c>TypeInitializationException -> DllNotFoundException</c> (issue #2).
/// WPF drawing has no native dependency of its own beyond WPF itself.
/// </remarks>
public static class FlagIcons
{
    private const int W = 24, H = 16;
    private static readonly Dictionary<string, BitmapSource?> Cache = new();

    /// <summary>
    /// The flag for a language code, or <c>null</c> when it could not be drawn. Callers show
    /// the language without an icon rather than failing: a decorative 24x16 bitmap is never
    /// worth taking the settings window down for.
    /// </summary>
    public static BitmapSource? Get(string code)
    {
        if (Cache.TryGetValue(code, out var cached)) return cached;

        BitmapSource? icon;
        try { icon = Render(code); }
        catch { icon = null; }

        Cache[code] = icon;   // cache the failure too, so a broken icon is not retried per item
        return icon;
    }

    private static BitmapSource Render(string code)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            Fill(dc, new Rect(0, 0, W, H), Brushes.White);
            switch (code)
            {
                case "tw": DrawTw(dc); break;
                case "us": DrawUs(dc); break;
                case "jp": DrawJp(dc); break;
                case "kr": DrawKr(dc); break;
                case "fr": DrawVertical(dc, Rgb(0x00, 0x35, 0x8E), Brushes.White, Rgb(0xED, 0x29, 0x39)); break;
                case "de": DrawHorizontal(dc, Brushes.Black, Rgb(0xDD, 0x00, 0x00), Rgb(0xFF, 0xCE, 0x00)); break;
                case "es": DrawEs(dc); break;
                default: Fill(dc, new Rect(0, 0, W, H), Rgb(0x80, 0x80, 0x80)); break;
            }
        }

        var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static SolidColorBrush Rgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static void Fill(DrawingContext dc, Rect r, Brush color) => dc.DrawRectangle(color, null, r);

    private static void Circle(DrawingContext dc, double cx, double cy, double rad, Brush color) =>
        dc.DrawEllipse(color, null, new Point(cx, cy), rad, rad);

    private static void DrawVertical(DrawingContext dc, Brush a, Brush b, Brush d)
    {
        Fill(dc, new Rect(0, 0, W / 3.0, H), a);
        Fill(dc, new Rect(W / 3.0, 0, W / 3.0, H), b);
        Fill(dc, new Rect(2 * W / 3.0, 0, W / 3.0, H), d);
    }

    private static void DrawHorizontal(DrawingContext dc, Brush a, Brush b, Brush d)
    {
        Fill(dc, new Rect(0, 0, W, H / 3.0), a);
        Fill(dc, new Rect(0, H / 3.0, W, H / 3.0), b);
        Fill(dc, new Rect(0, 2 * H / 3.0, W, H / 3.0), d);
    }

    private static void DrawEs(DrawingContext dc)
    {
        Fill(dc, new Rect(0, 0, W, H), Rgb(0xAA, 0x15, 0x1B));
        Fill(dc, new Rect(0, H * 0.25, W, H * 0.5), Rgb(0xF1, 0xBF, 0x00));
    }

    private static void DrawJp(DrawingContext dc) =>
        Circle(dc, W / 2.0, H / 2.0, H * 0.3, Rgb(0xBC, 0x00, 0x2D));

    private static void DrawKr(DrawingContext dc)
    {
        double cx = W / 2.0, cy = H / 2.0, r = H * 0.3;
        Circle(dc, cx, cy, r, Rgb(0xC6, 0x0C, 0x30));   // whole disc red

        // Bottom half blue: an arc sweeping from the right of the disc round the bottom to
        // the left, closed back across the diameter.
        var figure = new PathFigure { StartPoint = new Point(cx + r, cy), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(new Point(cx - r, cy), new Size(r, r),
            rotationAngle: 0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false));
        var half = new PathGeometry();
        half.Figures.Add(figure);
        half.Freeze();
        dc.DrawGeometry(Rgb(0x00, 0x38, 0x97), null, half);
    }

    private static void DrawTw(DrawingContext dc)
    {
        Fill(dc, new Rect(0, 0, W, H), Rgb(0xFE, 0x00, 0x00));               // red field
        Fill(dc, new Rect(0, 0, W / 2.0, H / 2.0), Rgb(0x00, 0x00, 0x95));   // blue canton
        Circle(dc, W / 4.0, H / 4.0, H * 0.16, Brushes.White);               // white sun (simplified)
    }

    private static void DrawUs(DrawingContext dc)
    {
        var red = Rgb(0xB2, 0x22, 0x34);
        for (int i = 0; i < 7; i++)
            Fill(dc, new Rect(0, i * H / 6.5, W, H / 13.0), red);            // red stripes
        Fill(dc, new Rect(0, 0, W * 0.42, H * 0.54), Rgb(0x3C, 0x3B, 0x6E));  // blue canton
    }
}
