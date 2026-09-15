using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScreenPaste.Editor;

namespace ScreenPaste.Rendering;

/// <summary>
/// How a magnifier ("局部放大") annotation was drawn; parked on the host's Tag.
/// <c>Source</c> is the framed area in region-local pixels — the annotation keeps showing
/// *that* area no matter where its enlarged view is later dragged.
/// </summary>
public sealed record MagnifySpec(
    Rect Source,
    ShapeKind Shape,
    double Zoom,
    double BorderThickness,
    Color BorderColor,
    bool Connector,
    bool Shadow,
    bool Smooth,
    bool IncludeAnnotations);

/// <summary>
/// Builds the visual for a magnifier annotation: the framed source area, an optional
/// connector to it, and the enlarged view of its pixels.
///
/// The host is a <see cref="Canvas"/> sized to the *enlarged view*, so hit-testing and the
/// selection box cover only that; the frame and connector are decoration that hangs outside
/// the host bounds, which a Canvas does not clip. Layout of the three children:
/// <code>
/// host  (Canvas.Left/Top = enlarged view position, Width/Height = view size)
/// -- [0] Path    frame around Source + connector, in host-local coords
/// -- [1] Canvas  clipped to the chosen shape, holding the enlarged Image
/// -- [2] Shape   the border drawn on top of the enlarged view
/// </code>
/// </summary>
public static class MagnifyEffects
{
    private const double Gap = 16;          // default distance from the framed source
    public const double MinZoom = 1.2;
    public const double MaxZoom = 10.0;

    /// <summary>Create the visual and auto-place its enlarged view next to the framed
    /// source, inside <paramref name="selection"/>. Call <see cref="Resample"/> to fill it.</summary>
    public static FrameworkElement Create(MagnifySpec spec, Rect selection)
    {
        var host = new Canvas();

        // Index 0 first so the connector runs *under* the enlarged view.
        var deco = new Path { IsHitTestVisible = false, Stretch = Stretch.None };
        Canvas.SetLeft(deco, 0);
        Canvas.SetTop(deco, 0);
        host.Children.Add(deco);

        var clip = new Canvas();
        clip.Children.Add(new Image { Stretch = Stretch.Fill });
        host.Children.Add(clip);

        host.Children.Add(spec.Shape == ShapeKind.Ellipse ? new Ellipse() : new Rectangle());

        var pos = Place(spec, selection);
        Canvas.SetLeft(host, pos.X);
        Canvas.SetTop(host, pos.Y);
        ApplySpec(host, spec);
        return host;
    }

    public static MagnifySpec? SpecOf(FrameworkElement host) =>
        host is Canvas { Tag: MagnifySpec s } ? s : null;

    /// <summary>Re-read the framed pixels out of <paramref name="sample"/> (a region-local
    /// bitmap: either the composite beneath the magnifier layer or the bare screenshot) and
    /// refresh the decoration. Safe to call on every frame.</summary>
    public static void Resample(FrameworkElement host, BitmapSource sample)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec spec) return;
        if (ViewImage(c) is not { } img) return;

        var want = new Int32Rect(
            (int)Math.Round(spec.Source.X),
            (int)Math.Round(spec.Source.Y),
            Math.Max(1, (int)Math.Round(spec.Source.Width)),
            Math.Max(1, (int)Math.Round(spec.Source.Height)));
        img.Source = LayerSampler.Crop(sample, want);
        UpdateDecoration(host);
    }

    /// <summary>
    /// Re-draw the frame and connector. The enlarged view moves with a drag while the framed
    /// source stays pinned to the content, so the decoration is rebuilt each frame.
    /// </summary>
    public static void UpdateDecoration(FrameworkElement host)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec spec) return;
        if (c.Children.Count < 1 || c.Children[0] is not Path deco) return;

        var size = ViewSize(spec);
        var pos = HostPosition(c);
        var src = new Rect(spec.Source.X - pos.X, spec.Source.Y - pos.Y,
            spec.Source.Width, spec.Source.Height);        // source in host-local coords
        var view = new Rect(0, 0, size.Width, size.Height);

        double t = Math.Max(1.0, spec.BorderThickness * 0.6);
        var group = new GeometryGroup();
        group.Children.Add(ShapeGeometry(src, spec.Shape));
        // A connector is noise when the enlarged view already covers its own source.
        if (spec.Connector && !src.IntersectsWith(view))
        {
            group.Children.Add(new LineGeometry(
                EdgePoint(src, spec.Shape, Center(view)),
                EdgePoint(view, spec.Shape, Center(src))));
        }

        // Frozen: UpdateDecoration runs on every drag frame, and a frozen Geometry/Brush
        // lets WPF cache the render data instead of re-validating it each time.
        group.Freeze();
        var stroke = new SolidColorBrush(spec.BorderColor);
        stroke.Freeze();
        deco.Data = group;
        deco.Stroke = stroke;
        deco.StrokeThickness = t;
    }

    /// <summary>Keep the framed area pinned to the screenshot content while the selection
    /// moves (the host itself is translated by the caller, like every other annotation).</summary>
    public static void ShiftSource(FrameworkElement host, double dx, double dy)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return;
        MoveSourceTo(host, new Point(s.Source.X + dx, s.Source.Y + dy));
    }

    /// <summary>
    /// Re-point the annotation at another area of the same size, in region-local coords. The
    /// enlarged view stays put, so the caller re-samples afterwards for the new content to
    /// show up. Takes a <see cref="Point"/> so that "the size is unchanged" is a fact of the
    /// signature rather than a trap for the caller; use <see cref="ResizeSource"/> to resize.
    /// </summary>
    public static void MoveSourceTo(FrameworkElement host, Point topLeft)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return;
        c.Tag = s with { Source = new Rect(topLeft.X, topLeft.Y, s.Source.Width, s.Source.Height) };
        UpdateDecoration(host);
    }

    /// <summary>Change the zoom factor, growing/shrinking the enlarged view about its centre.
    /// Returns false when it was already at that (clamped) zoom.</summary>
    public static bool SetZoom(FrameworkElement host, double zoom)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return false;
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(zoom - s.Zoom) < 0.001) return false;

        Reframe(c, s, s with { Zoom = zoom });
        return true;
    }

    /// <summary>
    /// Re-frame the annotation: unlike <see cref="MoveSourceTo"/> this takes the new size too,
    /// so the enlarged view resizes with it (view = source × zoom). Re-sample afterwards.
    /// </summary>
    public static void ResizeSource(FrameworkElement host, Rect source)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return;
        Reframe(c, s, s with { Source = source });
    }

    /// <summary>Where the enlarged view currently sits, translation included.</summary>
    public static Point ViewPosition(FrameworkElement host) =>
        host is Canvas c ? HostPosition(c) : new Point();

    /// <summary>Put the framed source and the enlarged view back at exact values — undo/redo
    /// of a re-frame, where re-deriving the position could drift once clamping is involved.</summary>
    public static void SetFrame(FrameworkElement host, Rect source, Point viewPosition)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return;
        ApplySpec(c, s with { Source = source });
        MoveViewTo(c, viewPosition);
    }

    /// <summary>
    /// Nudge the enlarged view back inside <paramref name="bounds"/> when it still fits, so
    /// growing the framed source (or zooming in) does not shove the view off the capture.
    /// A view too big to fit is left alone — there is nowhere better to put it.
    /// </summary>
    public static void ClampViewInto(FrameworkElement host, Rect bounds)
    {
        if (host is not Canvas c || c.Tag is not MagnifySpec s) return;
        var size = ViewSize(s);
        if (size.Width > bounds.Width || size.Height > bounds.Height) return;

        var at = HostPosition(c);
        MoveViewTo(c, new Point(
            Math.Clamp(at.X, bounds.X, bounds.Right - size.Width),
            Math.Clamp(at.Y, bounds.Y, bounds.Bottom - size.Height)));
    }

    /// <summary>Move the view to an absolute position, leaving the user's drag translation
    /// intact (it is folded into the Canvas offset instead).</summary>
    private static void MoveViewTo(Canvas c, Point position)
    {
        double tx = 0, ty = 0;
        if (c.RenderTransform is TranslateTransform tt) { tx = tt.X; ty = tt.Y; }
        Canvas.SetLeft(c, position.X - tx);
        Canvas.SetTop(c, position.Y - ty);
        UpdateDecoration(c);
    }

    /// <summary>Apply a spec whose view size may differ, keeping the view centred where it is
    /// so it does not walk away from the spot the user put it in. Reversible: re-applying the
    /// old spec re-centres about the same point.</summary>
    private static void Reframe(Canvas c, MagnifySpec before, MagnifySpec after)
    {
        var sizeBefore = ViewSize(before);
        var sizeAfter = ViewSize(after);

        double x = Canvas.GetLeft(c), y = Canvas.GetTop(c);
        Canvas.SetLeft(c, (double.IsNaN(x) ? 0 : x) - (sizeAfter.Width - sizeBefore.Width) / 2);
        Canvas.SetTop(c, (double.IsNaN(y) ? 0 : y) - (sizeAfter.Height - sizeBefore.Height) / 2);

        ApplySpec(c, after);
    }

    internal static Size ViewSize(MagnifySpec spec) => new(
        Math.Max(8, Math.Round(spec.Source.Width * spec.Zoom)),
        Math.Max(8, Math.Round(spec.Source.Height * spec.Zoom)));

    // ------------------------------------------------------------- internals ---

    private static void ApplySpec(FrameworkElement host, MagnifySpec spec)
    {
        if (host is not Canvas c) return;
        c.Tag = spec;

        var size = ViewSize(spec);
        c.Width = size.Width;
        c.Height = size.Height;

        if (c.Children.Count > 1 && c.Children[1] is Canvas clip)
        {
            clip.Width = size.Width;
            clip.Height = size.Height;
            clip.Clip = ShapeGeometry(new Rect(0, 0, size.Width, size.Height), spec.Shape);
            clip.Effect = spec.Shadow
                ? new DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 4,
                    Direction = 315,
                    Opacity = 0.5,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Performance,
                }
                : null;

            if (ViewImage(c) is { } img)
            {
                img.Width = size.Width;
                img.Height = size.Height;
                // Smoothing is what makes an enlarged screenshot legible; off gives the hard
                // pixel grid, which is what you want when inspecting individual pixels.
                RenderOptions.SetBitmapScalingMode(img,
                    spec.Smooth ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            }
        }

        if (c.Children.Count > 2 && c.Children[2] is Shape outline)
        {
            double t = Math.Max(0, spec.BorderThickness);
            outline.Stroke = t > 0 ? new SolidColorBrush(spec.BorderColor) : null;
            outline.StrokeThickness = t;
            outline.Fill = null;
            // Inset by half the stroke so the frame sits fully inside the enlarged view.
            outline.Width = Math.Max(0, size.Width - t);
            outline.Height = Math.Max(0, size.Height - t);
            Canvas.SetLeft(outline, t / 2);
            Canvas.SetTop(outline, t / 2);
            if (outline is Rectangle rect)
                rect.RadiusX = rect.RadiusY =
                    spec.Shape == ShapeKind.RoundedRectangle ? CornerRadius(size) : 0;
        }

        UpdateDecoration(host);
    }

    private static Image? ViewImage(Canvas host) =>
        host.Children.Count > 1 && host.Children[1] is Canvas { Children.Count: > 0 } clip
            ? clip.Children[0] as Image
            : null;

    /// <summary>First side of the framed source with room for the enlarged view inside the
    /// selection (right, below, left, above), else clamped next to it.</summary>
    internal static Point Place(MagnifySpec spec, Rect selection)
    {
        var s = spec.Source;
        var size = ViewSize(spec);
        double w = size.Width, h = size.Height;

        var candidates = new[]
        {
            new Point(s.Right + Gap, s.Y + s.Height / 2 - h / 2),
            new Point(s.X + s.Width / 2 - w / 2, s.Bottom + Gap),
            new Point(s.X - Gap - w, s.Y + s.Height / 2 - h / 2),
            new Point(s.X + s.Width / 2 - w / 2, s.Y - Gap - h),
        };

        foreach (var p in candidates)
            if (p.X >= selection.X && p.Y >= selection.Y &&
                p.X + w <= selection.Right && p.Y + h <= selection.Bottom)
                return p;

        return new Point(
            Math.Clamp(candidates[0].X, selection.X, Math.Max(selection.X, selection.Right - w)),
            Math.Clamp(candidates[0].Y, selection.Y, Math.Max(selection.Y, selection.Bottom - h)));
    }

    private static Point HostPosition(Canvas c)
    {
        double x = Canvas.GetLeft(c), y = Canvas.GetTop(c);
        if (double.IsNaN(x)) x = 0;
        if (double.IsNaN(y)) y = 0;
        if (c.RenderTransform is TranslateTransform tt) { x += tt.X; y += tt.Y; }
        return new Point(x, y);
    }

    private static double CornerRadius(Size size) =>
        Math.Min(14, Math.Min(size.Width, size.Height) / 6);

    private static Geometry ShapeGeometry(Rect r, ShapeKind shape)
    {
        if (shape == ShapeKind.Ellipse) return new EllipseGeometry(r);
        if (shape == ShapeKind.RoundedRectangle)
        {
            double radius = CornerRadius(new Size(r.Width, r.Height));
            return new RectangleGeometry(r, radius, radius);
        }
        return new RectangleGeometry(r);
    }

    private static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>Where the segment from the centre of <paramref name="r"/> towards
    /// <paramref name="toward"/> leaves its outline.</summary>
    internal static Point EdgePoint(Rect r, ShapeKind shape, Point toward)
    {
        var c = Center(r);
        double dx = toward.X - c.X, dy = toward.Y - c.Y;
        double a = r.Width / 2, b = r.Height / 2;
        if (a <= 0 || b <= 0 || (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6)) return c;

        if (shape == ShapeKind.Ellipse)
        {
            double d = Math.Sqrt(dx * dx / (a * a) + dy * dy / (b * b));
            return new Point(c.X + dx / d, c.Y + dy / d);
        }

        double tx = Math.Abs(dx) < 1e-6 ? double.PositiveInfinity : a / Math.Abs(dx);
        double ty = Math.Abs(dy) < 1e-6 ? double.PositiveInfinity : b / Math.Abs(dy);
        double t = Math.Min(tx, ty);
        return new Point(c.X + dx * t, c.Y + dy * t);
    }
}
