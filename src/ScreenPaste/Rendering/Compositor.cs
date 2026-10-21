using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Native;

namespace ScreenPaste.Rendering;

/// <summary>The captured mouse pointer, drawn as part of the screenshot: its image (96 DPI, so
/// one unit is one pixel) and its top-left in the screenshot's px.</summary>
public readonly record struct CursorLayer(BitmapSource Image, Point Position);

/// <summary>Flattens the screenshot crop + blur layer + ink strokes into one frozen bitmap.</summary>
public static class Compositor
{
    /// <summary>
    /// Everything that sits *under* the blur layer, at 1:1 physical-pixel resolution.
    /// This is what a blur/mosaic region samples, so regions obscure the annotations
    /// beneath them instead of re-exposing the untouched screenshot.
    /// </summary>
    public static BitmapSource ComposeBeneathBlur(BitmapSource screenshot, Int32Rect regionPx, CursorLayer? cursor,
        StrokeCollection strokes, Visual shapeLayer, Visual stickerLayer, Visual textLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            DrawBase(dc, screenshot, regionPx, cursor, full);    // 1) base screenshot (and pointer)
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
    /// <paramref name="cursor"/> is the captured pointer when it is shown (part of the base);
    /// <paramref name="strokes"/> are the pen/highlighter strokes in region-local coords;
    /// <paramref name="blurLayer"/> is the blur-region host (positioned at region origin).
    /// The blur layer goes on top so a region hides whatever was drawn under it, and the
    /// magnifier layer on top of that so an enlarged view is never itself obscured.
    /// </summary>
    public static BitmapSource Compose(BitmapSource screenshot, Int32Rect regionPx, CursorLayer? cursor,
        StrokeCollection strokes, Visual blurLayer, Visual magnifyLayer,
        Visual shapeLayer, Visual stickerLayer, Visual textLayer)
    {
        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var full = new Rect(0, 0, w, h);

        var beneath = ComposeBeneathMagnify(
            ComposeBeneathBlur(screenshot, regionPx, cursor, strokes, shapeLayer, stickerLayer, textLayer),
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

    /// <summary>The bare screenshot crop for the selection (with the pointer, when it is shown
    /// and over the area), in region-local coords.</summary>
    public static BitmapSource CropRegion(BitmapSource screenshot, Int32Rect regionPx, CursorLayer? cursor)
    {
        if (CursorIn(cursor, regionPx) == null)
        {
            var crop = new CroppedBitmap(screenshot, ClampRect(regionPx, screenshot));
            crop.Freeze();
            return crop;
        }

        int w = Math.Max(1, regionPx.Width);
        int h = Math.Max(1, regionPx.Height);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) DrawBase(dc, screenshot, regionPx, cursor, new Rect(0, 0, w, h));

        var result = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        result.Render(dv);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// <paramref name="screenshot"/> with <paramref name="region"/> written over it at
    /// <paramref name="at"/>, pixel for pixel (clipped to the screenshot), frozen: a replayed
    /// snip's flattened area back on the screen it was cut from.
    /// </summary>
    public static BitmapSource PasteRegion(BitmapSource screenshot, BitmapSource region, Int32Rect at)
    {
        var target = new WriteableBitmap(Pbgra32(screenshot));
        int x = Math.Clamp(at.X, 0, target.PixelWidth);
        int y = Math.Clamp(at.Y, 0, target.PixelHeight);
        int w = Math.Min(at.X + Math.Min(at.Width, region.PixelWidth), target.PixelWidth) - x;
        int h = Math.Min(at.Y + Math.Min(at.Height, region.PixelHeight), target.PixelHeight) - y;
        if (w > 0 && h > 0)
        {
            var source = Pbgra32(region);
            var pixels = new byte[w * h * 4];
            source.CopyPixels(new Int32Rect(x - at.X, y - at.Y, w, h), pixels, w * 4, 0);
            target.WritePixels(new Int32Rect(x, y, w, h), pixels, w * 4, 0);
        }
        target.Freeze();
        return target;
    }

    private static BitmapSource Pbgra32(BitmapSource bmp) =>
        bmp.Format == PixelFormats.Pbgra32 ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Pbgra32, null, 0);

    /// <summary>The screenshot crop, then the pointer where it falls over the area.</summary>
    private static void DrawBase(DrawingContext dc, BitmapSource screenshot, Int32Rect regionPx, CursorLayer? cursor, Rect full)
    {
        dc.DrawImage(new CroppedBitmap(screenshot, ClampRect(regionPx, screenshot)), full);
        if (cursor is { } c && CursorIn(cursor, regionPx) is { } at) dc.DrawImage(c.Image, at);
    }

    private static Rect? CursorIn(CursorLayer? cursor, Int32Rect regionPx) => cursor is { } c
        ? CursorPlacement.InRegion(c.Position, c.Image.PixelWidth, c.Image.PixelHeight, regionPx)
        : null;

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
