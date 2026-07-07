using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace ScreenPaste.Settings;

/// <summary>Small simplified flag icons drawn with SkiaSharp, for the language combo.</summary>
public static class FlagIcons
{
    private const int W = 24, H = 16;
    private static readonly Dictionary<string, BitmapSource> Cache = new();

    public static BitmapSource Get(string code)
    {
        if (Cache.TryGetValue(code, out var cached)) return cached;
        var bs = Render(code);
        Cache[code] = bs;
        return bs;
    }

    private static BitmapSource Render(string code)
    {
        var info = new SKImageInfo(W, H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            switch (code)
            {
                case "tw": DrawTw(c); break;
                case "us": DrawUs(c); break;
                case "jp": DrawJp(c); break;
                case "kr": DrawKr(c); break;
                case "fr": DrawVertical(c, new(0x00, 0x35, 0x8E), SKColors.White, new(0xED, 0x29, 0x39)); break;
                case "de": DrawHorizontal(c, SKColors.Black, new(0xDD, 0x00, 0x00), new(0xFF, 0xCE, 0x00)); break;
                case "es": DrawEs(c); break;
                default: c.Clear(new SKColor(0x80, 0x80, 0x80)); break;
            }
        }
        var src = BitmapSource.Create(W, H, 96, 96, PixelFormats.Pbgra32, null, bmp.Bytes, bmp.RowBytes);
        src.Freeze();
        return src;
    }

    private static void Fill(SKCanvas c, SKRect r, SKColor color)
    {
        using var p = new SKPaint { Color = color, IsAntialias = false };
        c.DrawRect(r, p);
    }

    private static void Circle(SKCanvas c, float cx, float cy, float rad, SKColor color)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true };
        c.DrawCircle(cx, cy, rad, p);
    }

    private static void DrawVertical(SKCanvas c, SKColor a, SKColor b, SKColor d)
    {
        Fill(c, new SKRect(0, 0, W / 3f, H), a);
        Fill(c, new SKRect(W / 3f, 0, 2 * W / 3f, H), b);
        Fill(c, new SKRect(2 * W / 3f, 0, W, H), d);
    }

    private static void DrawHorizontal(SKCanvas c, SKColor a, SKColor b, SKColor d)
    {
        Fill(c, new SKRect(0, 0, W, H / 3f), a);
        Fill(c, new SKRect(0, H / 3f, W, 2 * H / 3f), b);
        Fill(c, new SKRect(0, 2 * H / 3f, W, H), d);
    }

    private static void DrawEs(SKCanvas c)
    {
        var red = new SKColor(0xAA, 0x15, 0x1B);
        Fill(c, new SKRect(0, 0, W, H), red);
        Fill(c, new SKRect(0, H * 0.25f, W, H * 0.75f), new SKColor(0xF1, 0xBF, 0x00));
    }

    private static void DrawJp(SKCanvas c) => Circle(c, W / 2f, H / 2f, H * 0.3f, new SKColor(0xBC, 0x00, 0x2D));

    private static void DrawKr(SKCanvas c)
    {
        float cx = W / 2f, cy = H / 2f, r = H * 0.3f;
        Circle(c, cx, cy, r, new SKColor(0xC6, 0x0C, 0x30));                 // top red
        using var p = new SKPaint { Color = new SKColor(0x00, 0x38, 0x97), IsAntialias = true };
        using var path = new SKPath();
        path.AddArc(new SKRect(cx - r, cy - r, cx + r, cy + r), 0, 180);     // bottom half blue
        c.DrawPath(path, p);
    }

    private static void DrawTw(SKCanvas c)
    {
        Fill(c, new SKRect(0, 0, W, H), new SKColor(0xFE, 0x00, 0x00));      // red field
        Fill(c, new SKRect(0, 0, W / 2f, H / 2f), new SKColor(0x00, 0x00, 0x95)); // blue canton
        Circle(c, W / 4f, H / 4f, H * 0.16f, SKColors.White);               // white sun (simplified)
    }

    private static void DrawUs(SKCanvas c)
    {
        var red = new SKColor(0xB2, 0x22, 0x34);
        for (int i = 0; i < 7; i++)
            Fill(c, new SKRect(0, i * H / 6.5f, W, i * H / 6.5f + H / 13f), red); // red stripes
        Fill(c, new SKRect(0, 0, W * 0.42f, H * 0.54f), new SKColor(0x3C, 0x3B, 0x6E)); // blue canton
    }
}
