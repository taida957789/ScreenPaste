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

    private static (int Width, int Height) Turned(int width, int height, int rotation) =>
        rotation % 180 == 0 ? (width, height) : (height, width);
}
