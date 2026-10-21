using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenPaste.Native;

namespace ScreenPaste.Output;

/// <summary>
/// Thumbnail mode (Snipaste): Shift+double-click shrinks the pin to a small square showing
/// the middle of its image, centred on the cursor so the next Shift+double-click finds it,
/// and a right-drag over the pin keeps just the part dragged over, where it is (a left click
/// during the drag cancels it; a right click without a drag still opens the menu). The same
/// Shift+double-click, or the menu, brings the whole pin back as it was, moved as far as the
/// thumbnail was dragged. A thumbnail drags, fades, recolours, copies, saves and closes as
/// the pin does; a turn, a flip, a zoom or new content brings the whole pin back first.
/// </summary>
public sealed partial class PinWindow
{
    private static readonly SolidColorBrush MarqueeStroke = CreateFrameBrush(Color.FromRgb(0x3D, 0xA9, 0xFC));
    private static readonly SolidColorBrush MarqueeFill = CreateFrameBrush(Color.FromArgb(0x33, 0x3D, 0xA9, 0xFC));

    private PinThumbnailView? _thumbnail;
    private Point _thumbnailFrom;              // the whole image's top-left when the thumbnail came
    private Point _thumbnailAt;                // the thumbnail's top-left when it came
    private Point? _rightDragStart;            // the cursor when the right button went down, while it is held
    private bool _swallowRightUp;              // a left click cancelled the right-drag: no menu on release
    private Rectangle _marquee = null!;        // the part a right-drag covers

    private void InitThumbnail()
    {
        _marquee = new Rectangle
        {
            Stroke = MarqueeStroke,
            Fill = MarqueeFill,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _viewport.Children.Add(_marquee);   // over the image
        MouseRightButtonDown += OnRightDown;
        MouseRightButtonUp += OnRightUp;
        LostMouseCapture += (_, _) => DropRightDrag();
    }

    /// <summary>Shift+double-click (a plain one too when PinDoubleClickAction says so) and the
    /// menu: into thumbnail mode, or back out of it.</summary>
    private void ToggleThumbnail()
    {
        if (_thumbnail != null)
        {
            ExitThumbnail();
            return;
        }
        var image = ImageRect();
        var (w, h) = TurnedSourceSize();
        int side = (int)Math.Round(PinThumbnail.ClampSide(_settings.PinThumbnailSize) * _dpi);
        var view = PinThumbnail.Square(w, h, _state.Zoom, side);
        var cursor = NativeMethods.GetCursorPos(out var pt) ? new Point(pt.X, pt.Y) : Centre(image);
        var (tw, th) = view.OutputSize;
        EnterThumbnail(view, PinThumbnail.PlaceAt(image, cursor, tw, th));
    }

    /// <summary>Show <paramref name="view"/> with its top-left at <paramref name="topLeft"/>.</summary>
    private void EnterThumbnail(PinThumbnailView view, Point topLeft)
    {
        if (_thumbnail != null || _closed) return;
        _thumbnailFrom = ImageRect().TopLeft;   // read while it still measures the whole image
        _thumbnailAt = topLeft;
        _thumbnail = view;
        Cursor = null;                          // a thumbnail has no border to resize
        Present(_state, topLeft);
    }

    /// <summary>The whole pin again, moved as far as the thumbnail was; nothing outside thumbnail mode.</summary>
    private void ExitThumbnail()
    {
        if (_thumbnail == null) return;
        var back = FullImageTopLeft();
        _thumbnail = null;
        Present(_state, back);
    }

    /// <summary>Where the whole image's top-left is, or comes back to from the thumbnail.</summary>
    private Point FullImageTopLeft() => _thumbnail == null
        ? ImageRect().TopLeft
        : PinThumbnail.RestoreTopLeft(_thumbnailFrom, _thumbnailAt, ImageRect().TopLeft);

    // -------------------------------------------------- selection thumbnail ---

    /// <summary>The right button starts a drag over the whole pin; on a thumbnail, during a
    /// left drag or while the pin lets clicks through it is only the menu's.</summary>
    private void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        _swallowRightUp = false;
        if (_thumbnail != null || _move != null || _resize != null) return;
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        _rightDragStart = new Point(pt.X, pt.Y);
        if (!CaptureMouse()) _rightDragStart = null;
    }

    private void ContinueRightDrag()
    {
        if (_rightDragStart is not { } start || !NativeMethods.GetCursorPos(out var pt)) return;
        if (DraggedPart(start, new Point(pt.X, pt.Y)) is not { } part)
        {
            _marquee.Visibility = Visibility.Collapsed;
            return;
        }
        Canvas.SetLeft(_marquee, part.X / _dpi);
        Canvas.SetTop(_marquee, part.Y / _dpi);
        _marquee.Width = part.Width / _dpi;
        _marquee.Height = part.Height / _dpi;
        _marquee.StrokeThickness = 1 / _dpi;
        _marquee.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The release: after a drag the pin becomes a thumbnail of the part dragged over, at the
    /// size and place it has now, and the menu stays shut; after a click (under
    /// <see cref="PinThumbnail.MinDrag"/> px) the menu opens as it always did.
    /// </summary>
    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (_swallowRightUp)
        {
            _swallowRightUp = false;
            e.Handled = true;
            return;
        }
        if (_rightDragStart is not { } start) return;
        var now = NativeMethods.GetCursorPos(out var pt) ? new Point(pt.X, pt.Y) : start;
        var part = DraggedPart(start, now);
        DropRightDrag();
        if (!PinThumbnail.IsDrag(now - start)) return;

        e.Handled = true;   // keeps the menu shut
        if (part is not { } p) return;
        var (w, h) = TurnedSourceSize();
        var crop = PinThumbnail.FromDrag(p, _state.Zoom, w, h);
        var (sx, sy) = DisplayScale();
        EnterThumbnail(new PinThumbnailView(crop, _state.Zoom), ImageRect().TopLeft + new Vector(crop.X * sx, crop.Y * sy));
    }

    /// <summary>The part of the image between <paramref name="start"/> and
    /// <paramref name="now"/> (physical px from the image's top-left), or null while the
    /// button has not moved far enough to be a drag or the part misses the image.</summary>
    private Rect? DraggedPart(Point start, Point now)
    {
        if (!PinThumbnail.IsDrag(now - start)) return null;
        var image = ImageRect();
        var origin = (Vector)image.TopLeft;
        var part = new Rect(start - origin, now - origin);
        part.Intersect(new Rect(0, 0, image.Width, image.Height));
        return part.IsEmpty ? null : part;
    }

    /// <summary>A left press during a right-drag: no thumbnail, and no menu when the right
    /// button comes up. False when no right-drag is under way.</summary>
    private bool CancelRightDrag()
    {
        if (_rightDragStart == null) return false;
        DropRightDrag();
        _swallowRightUp = true;
        return true;
    }

    private void DropRightDrag()
    {
        if (_rightDragStart == null) return;
        _rightDragStart = null;
        _marquee.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured && _move == null && _resize == null) ReleaseMouseCapture();
    }
}
