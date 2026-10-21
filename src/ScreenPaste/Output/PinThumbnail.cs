using System.Windows;

namespace ScreenPaste.Output;

/// <summary>
/// What a pin in thumbnail mode shows: <see cref="Crop"/>, a part of the image as it is
/// turned and flipped (image px), at <see cref="Scale"/> screen px per image px.
/// </summary>
public sealed record PinThumbnailView(Rect Crop, double Scale)
{
    /// <summary>The thumbnail's size on screen (physical px), never below one pixel.</summary>
    public (int Width, int Height) OutputSize =>
        (Math.Max(1, (int)Math.Round(Crop.Width * Scale)), Math.Max(1, (int)Math.Round(Crop.Height * Scale)));
}

/// <summary>
/// A pin's thumbnail mode (Snipaste): Shift+double-click shrinks the pin to a small square
/// showing the middle of its image, and a right-drag over the pin keeps just the dragged
/// part, where it is. Toggling back brings the whole pin back as it was, moved as far as the
/// thumbnail was dragged. Pure, so it is unit tested; screen positions are physical px.
/// </summary>
public static class PinThumbnail
{
    /// <summary>The square's side bounds (AppSettings.PinThumbnailSize, in px at 100% scaling).</summary>
    public const int MinSide = 16;
    public const int MaxSide = 512;

    /// <summary>A right-drag shorter than this along both axes is a click, which opens the menu.</summary>
    public const double MinDrag = 4;

    /// <summary>The settings value within <see cref="MinSide"/>-<see cref="MaxSide"/>.</summary>
    public static int ClampSide(int setting) => Math.Clamp(setting, MinSide, MaxSide);

    /// <summary>
    /// The largest part of a <paramref name="image"/>-sized image with the shape of
    /// <paramref name="thumb"/>, centred, on whole pixels. Without a thumb shape, the whole
    /// image; an empty image gives an empty rect.
    /// </summary>
    public static Rect CropRect(Size image, Size thumb)
    {
        if (image.IsEmpty || image.Width <= 0 || image.Height <= 0) return Rect.Empty;
        var whole = new Rect(0, 0, image.Width, image.Height);
        if (thumb.IsEmpty || thumb.Width <= 0 || thumb.Height <= 0) return whole;

        double aspect = thumb.Width / thumb.Height;
        double w = image.Width, h = image.Height;
        if (image.Width / image.Height > aspect) w = Math.Max(1, Math.Round(image.Height * aspect));
        else h = Math.Max(1, Math.Round(image.Width / aspect));
        return new Rect(Math.Floor((image.Width - w) / 2), Math.Floor((image.Height - h) / 2), w, h);
    }

    /// <summary>
    /// Shift+double-click: the centred square of a <paramref name="width"/> x
    /// <paramref name="height"/> image (as turned) shown at <paramref name="side"/> px, or at
    /// the pin's own shorter side when it is shown smaller than that, so it never grows.
    /// </summary>
    public static PinThumbnailView Square(int width, int height, double zoom, int side)
    {
        var crop = CropRect(new Size(Math.Max(1, width), Math.Max(1, height)), new Size(1, 1));
        int shown = Math.Max(1, (int)Math.Round(Math.Min(width, height) * zoom));
        int target = Math.Max(1, Math.Min(side, shown));
        return new PinThumbnailView(crop, target / crop.Width);
    }

    /// <summary>
    /// A right-drag over the pin (<paramref name="drag"/>, physical px from the image's
    /// top-left) as the image px it covers at <paramref name="zoom"/>: every pixel it touches,
    /// within the <paramref name="width"/> x <paramref name="height"/> image, at least one.
    /// </summary>
    public static Rect FromDrag(Rect drag, double zoom, int width, int height)
    {
        if (zoom <= 0) zoom = 1;
        int maxX = Math.Max(1, width), maxY = Math.Max(1, height);
        int left = Math.Clamp((int)Math.Floor(drag.Left / zoom), 0, maxX - 1);
        int top = Math.Clamp((int)Math.Floor(drag.Top / zoom), 0, maxY - 1);
        int right = Math.Clamp((int)Math.Ceiling(drag.Right / zoom), left + 1, maxX);
        int bottom = Math.Clamp((int)Math.Ceiling(drag.Bottom / zoom), top + 1, maxY);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>Whether the right button moved far enough to be a drag rather than a click.</summary>
    public static bool IsDrag(Vector moved) => Math.Abs(moved.X) >= MinDrag || Math.Abs(moved.Y) >= MinDrag;

    /// <summary>Where a <paramref name="width"/> x <paramref name="height"/> thumbnail goes:
    /// centred on the cursor, so a second Shift+double-click finds it, but kept over the pin
    /// (<paramref name="pin"/>, physical px).</summary>
    public static Point PlaceAt(Rect pin, Point cursor, int width, int height) => new(
        Math.Round(ClampTolerant(cursor.X - width / 2.0, pin.Left, pin.Right - width)),
        Math.Round(ClampTolerant(cursor.Y - height / 2.0, pin.Top, pin.Bottom - height)));

    /// <summary>Where the whole image's top-left goes when the thumbnail ends: where it was
    /// (<paramref name="before"/>), moved as far as the thumbnail moved since it came.</summary>
    public static Point RestoreTopLeft(Point before, Point thumbnailAtStart, Point thumbnailNow) =>
        before + (thumbnailNow - thumbnailAtStart);

    private static double ClampTolerant(double value, double min, double max) =>
        max < min ? min : Math.Clamp(value, min, max);
}
