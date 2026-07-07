using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Rendering;

/// <summary>Flattens the screenshot crop + blur layer + ink strokes into one frozen bitmap.</summary>
public static class Compositor
{
    /// <summary>
    /// Composite the final image at 1:1 physical-pixel resolution.
    /// <paramref name="screenshot"/> is the full virtual-screen capture;
    /// <paramref name="regionPx"/> is the selection in screenshot pixel coords;
    /// <paramref name="strokes"/> are the pen/highlighter strokes in region-local coords;
    /// <paramref name="blurLayer"/> is the blur-region host (positioned at region origin).
    /// </summary>
    public static BitmapSource Compose(BitmapSource screenshot, Int32Rect regionPx,
        StrokeCollection strokes, Visual blurLayer, Visual shapeLayer, Visual stickerLayer, Visual textLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        // Crop the underlying screenshot for the selection.
        var crop = new CroppedBitmap(screenshot, ClampRect(regionPx, screenshot));

        // The annotation hosts have offset (0,0), so they render 1:1 into the region.
        BitmapSource RenderLayer(Visual v)
        {
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(v);
            rtb.Freeze();
            return rtb;
        }

        var blurRtb = RenderLayer(blurLayer);
        var shapeRtb = RenderLayer(shapeLayer);
        var stickerRtb = RenderLayer(stickerLayer);
        var textRtb = RenderLayer(textLayer);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(crop, full);       // 1) base screenshot
            dc.DrawImage(blurRtb, full);    // 2) blur regions
            dc.DrawImage(shapeRtb, full);   // 3) shapes
            dc.DrawImage(stickerRtb, full); // 4) pasted image stickers
            strokes.Draw(dc);               // 5) ink strokes (region-local coords)
            dc.DrawImage(textRtb, full);    // 6) text annotations on top
        }

        var result = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        result.Render(dv);
        result.Freeze();
        return result;
    }

    private static Int32Rect ClampRect(Int32Rect r, BitmapSource src)
    {
        int x = Math.Clamp(r.X, 0, src.PixelWidth - 1);
        int y = Math.Clamp(r.Y, 0, src.PixelHeight - 1);
        int w = Math.Clamp(r.Width, 1, src.PixelWidth - x);
        int h = Math.Clamp(r.Height, 1, src.PixelHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}
