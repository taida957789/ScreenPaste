using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Rendering;

/// <summary>Flattens the screenshot crop + blur layer + ink strokes into one frozen bitmap.</summary>
public static class Compositor
{
    /// <summary>
    /// Everything that sits *under* the blur layer, at 1:1 physical-pixel resolution.
    /// This is what a blur/mosaic region samples, so regions obscure the annotations
    /// beneath them instead of re-exposing the untouched screenshot.
    /// </summary>
    public static BitmapSource ComposeBeneathBlur(BitmapSource screenshot, Int32Rect regionPx,
        StrokeCollection strokes, Visual shapeLayer, Visual stickerLayer, Visual textLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        // Crop the underlying screenshot for the selection.
        var crop = new CroppedBitmap(screenshot, ClampRect(regionPx, screenshot));

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(crop, full);                        // 1) base screenshot
            dc.DrawImage(RenderLayer(shapeLayer, w, h), full);   // 2) shapes
            dc.DrawImage(RenderLayer(stickerLayer, w, h), full); // 3) pasted image stickers
            strokes.Draw(dc);                                // 4) ink strokes (region-local coords)
            dc.DrawImage(RenderLayer(textLayer, w, h), full);    // 5) text annotations
        }

        var result = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        result.Render(dv);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Everything that sits *under* the magnifier layer: the blur layer flattened onto
    /// <paramref name="beneathBlur"/>. A magnifier set to include annotations samples this,
    /// so it enlarges blurred pixels as blurred.
    /// </summary>
    /// <param name="beneathBlur">
    /// The result of <see cref="ComposeBeneathBlur"/>, passed in rather than recomputed: the
    /// caller already caches it for the blur layer, and rebuilding it here would rasterise
    /// the shape/sticker/ink/text stack a second time on every edit.
    /// </param>
    public static BitmapSource ComposeBeneathMagnify(BitmapSource beneathBlur, Int32Rect regionPx,
        Visual blurLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(beneathBlur, full);
            dc.DrawImage(RenderLayer(blurLayer, w, h), full);
        }

        var result = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        result.Render(dv);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Composite the final image at 1:1 physical-pixel resolution.
    /// <paramref name="screenshot"/> is the full virtual-screen capture;
    /// <paramref name="regionPx"/> is the selection in screenshot pixel coords;
    /// <paramref name="strokes"/> are the pen/highlighter strokes in region-local coords;
    /// <paramref name="blurLayer"/> is the blur-region host (positioned at region origin).
    /// The blur layer goes on top so a region hides whatever was drawn under it, and the
    /// magnifier layer on top of that so an enlarged view is never itself obscured.
    /// </summary>
    public static BitmapSource Compose(BitmapSource screenshot, Int32Rect regionPx,
        StrokeCollection strokes, Visual blurLayer, Visual magnifyLayer,
        Visual shapeLayer, Visual stickerLayer, Visual textLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        var beneath = ComposeBeneathMagnify(
            ComposeBeneathBlur(screenshot, regionPx, strokes, shapeLayer, stickerLayer, textLayer),
            regionPx, blurLayer);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(beneath, full);
            dc.DrawImage(RenderLayer(magnifyLayer, w, h), full);
        }

        var result = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        result.Render(dv);
        result.Freeze();
        return result;
    }

    /// <summary>The bare screenshot crop for the selection, in region-local coords.</summary>
    public static BitmapSource CropRegion(BitmapSource screenshot, Int32Rect regionPx)
    {
        var crop = new CroppedBitmap(screenshot, ClampRect(regionPx, screenshot));
        crop.Freeze();
        return crop;
    }

    /// <summary>The annotation hosts have offset (0,0), so they render 1:1 into the region.</summary>
    private static BitmapSource RenderLayer(Visual v, int w, int h)
    {
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(v);
        rtb.Freeze();
        return rtb;
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
