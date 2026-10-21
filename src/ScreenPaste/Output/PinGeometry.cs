using System.Windows;

namespace ScreenPaste.Output;

/// <summary>
/// A pin's size and place on screen, in physical px. The window shows the image turned and
/// zoomed (<see cref="PinTransformState"/>); a zoom keeps the image point under the cursor
/// where it is (Snipaste's wheel zoom), a quarter turn keeps the centre. Pure, so it is
/// unit tested; <see cref="PinWindow"/> reads its real position back from the window
/// before every change, since it may have been dragged since.
/// </summary>
public static class PinGeometry
{
    /// <summary>The shown image's size: the sides swap on a quarter turn, then scale by the
    /// zoom, rounded to whole pixels and never below one.</summary>
    public static (int Width, int Height) OutputPixelSize(int width, int height, int rotation, double zoom)
    {
        var (w, h) = Turned(width, height, rotation);
        return (Math.Max(1, (int)Math.Round(w * zoom)), Math.Max(1, (int)Math.Round(h * zoom)));
    }

    /// <summary>Screen px per image px along each screen axis, so the turned image fills the
    /// rounded <see cref="OutputPixelSize"/> exactly (the zoom, give or take the rounding).</summary>
    public static (double X, double Y) PixelScale(int width, int height, int rotation, double zoom)
    {
        var (w, h) = Turned(Math.Max(1, width), Math.Max(1, height), rotation);
        var (outW, outH) = OutputPixelSize(width, height, rotation, zoom);
        return ((double)outW / w, (double)outH / h);
    }

    /// <summary>
    /// The pin's rect after zooming from <paramref name="oldZoom"/> to
    /// <paramref name="newZoom"/>, keeping the image point under <paramref name="cursor"/>
    /// in place. A cursor outside the pin (a key press, or the wheel reaching a pin that is
    /// not under it) anchors at the nearest point of its edge instead.
    /// </summary>
    public static Rect ZoomAt(Rect window, Point cursor, double oldZoom, double newZoom)
    {
        if (window.IsEmpty || oldZoom <= 0 || newZoom <= 0 || oldZoom == newZoom) return window;

        double ratio = newZoom / oldZoom;
        double ax = Math.Clamp(cursor.X, window.Left, window.Right);
        double ay = Math.Clamp(cursor.Y, window.Top, window.Bottom);
        return new Rect(
            ax - (ax - window.X) * ratio,
            ay - (ay - window.Y) * ratio,
            window.Width * ratio,
            window.Height * ratio);
    }

    /// <summary>A <paramref name="width"/> x <paramref name="height"/> rect with the same
    /// centre as <paramref name="window"/>: where a pin goes when a quarter turn swaps its sides.</summary>
    public static Rect Recentre(Rect window, int width, int height)
    {
        double cx = window.X + window.Width / 2, cy = window.Y + window.Height / 2;
        return new Rect(cx - width / 2.0, cy - height / 2.0, width, height);
    }

    /// <summary>
    /// Where the image's top-left goes when the pin changes to a <paramref name="width"/> x
    /// <paramref name="height"/> output: a zoom keeps the image point at
    /// <paramref name="anchor"/> still (<see cref="ZoomAt"/>), a quarter turn keeps the centre
    /// (<see cref="Recentre"/>). Only a turn recentres: recentring a zoom on the rounded size
    /// would move the anchored point by up to half a pixel a step.
    /// </summary>
    public static Point NextTopLeft(Rect window, Point anchor, double oldZoom, double newZoom,
        int width, int height, bool quarterTurn)
    {
        var zoomed = ZoomAt(window, anchor, oldZoom, newZoom);
        return quarterTurn ? Recentre(zoomed, width, height).TopLeft : zoomed.TopLeft;
    }

    /// <summary>Where a pasted pin's image goes: its top-left at <paramref name="cursor"/>,
    /// moved back onto <paramref name="monitor"/> where it would run off the right or bottom;
    /// one larger than the monitor starts at its top-left.</summary>
    public static Point PasteAt(Point cursor, int width, int height, Rect monitor)
    {
        double x = Math.Max(monitor.Left, Math.Min(cursor.X, monitor.Right - width));
        double y = Math.Max(monitor.Top, Math.Min(cursor.Y, monitor.Bottom - height));
        return new Point(x, y);
    }

    /// <summary>
    /// The image pixel the pin shows at <paramref name="offset"/> (physical px from the shown
    /// part's top-left), in px of the image as turned, for the pin's loupe: the shown part is
    /// <paramref name="shown"/> (the whole image, or a thumbnail's crop) at
    /// <paramref name="scaleX"/> x <paramref name="scaleY"/> screen px per image px. Null off it.
    /// </summary>
    public static (int X, int Y)? PixelAt(Point offset, double scaleX, double scaleY, Rect shown)
    {
        if (shown.IsEmpty || scaleX <= 0 || scaleY <= 0 || offset.X < 0 || offset.Y < 0) return null;
        int x = (int)Math.Floor(offset.X / scaleX), y = (int)Math.Floor(offset.Y / scaleY);
        if (x >= shown.Width || y >= shown.Height) return null;
        return (x + (int)shown.X, y + (int)shown.Y);
    }

    /// <summary>Where a <paramref name="width"/> x <paramref name="height"/> loupe goes:
    /// <paramref name="gap"/> px below and right of the cursor, flipped to the other side near
    /// the right or bottom of <paramref name="monitor"/>, and never off it (as the capture's).</summary>
    public static Point LoupeAt(Point cursor, double width, double height, Rect monitor, double gap)
    {
        double x = cursor.X + gap;
        if (x + width > monitor.Right) x = cursor.X - gap - width;
        double y = cursor.Y + gap;
        if (y + height > monitor.Bottom) y = cursor.Y - gap - height;
        return new Point(
            ClampTolerant(x, monitor.Left, monitor.Right - width),
            ClampTolerant(y, monitor.Top, monitor.Bottom - height));
    }

    private static double ClampTolerant(double value, double min, double max) =>
        max < min ? min : Math.Clamp(value, min, max);

    private static (int Width, int Height) Turned(int width, int height, int rotation) =>
        rotation % 180 == 0 ? (width, height) : (height, width);
}
