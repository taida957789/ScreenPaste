using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenPaste.Editor;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The eraser tool (E, Ctrl+9). Dragging it removes every ink stroke and annotation it passes
/// over: strokes, lines and shapes when it touches their outline (a filled shape anywhere
/// inside), everything else when it reaches their bounds. One drag is one undo step, and undo
/// puts everything back at its old depth. A right-click marks with it like a fill tool
/// (<see cref="RightClickPolicy"/>): it clears what overlaps the UI element under the cursor,
/// or everything when clicked outside the region.
///
/// The drag is handled on EditLayer's tunnelling events ahead of the annotation grab, so with
/// the eraser a press erases rather than picking an annotation up; a ring shows its size.
/// </summary>
public partial class CaptureOverlayWindow
{
    private static readonly string GlyphEraser = Glyph(0xE75C);   // EraseTool
    private static readonly SolidColorBrush EraserRingFill = new(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    /// <summary>One thing an eraser removed: a stroke, or an element of a host, and its index.</summary>
    private readonly record struct ErasedItem(Canvas? Host, UIElement? Element, Stroke? Stroke, int Index);

    private double _eraserWidth = 16;
    private Slider _eraserWidthSlider = null!;
    private StackPanel _eraserOptionsPanel = null!;
    private Ellipse? _eraserRing;            // the eraser's footprint under the pointer (never exported)
    private Point _eraserRingAt;
    private List<ErasedItem>? _erased;       // what the drag in progress removed, in order
    private Point _eraseLast;

    private void InitEraser()
    {
        var range = PenSizeStep.RangeOf(ToolKind.Eraser)!.Value;
        _eraserWidth = Math.Clamp(_settings.EraserWidth, range.Min, range.Max);

        EditLayer.PreviewMouseLeftButtonDown += Eraser_PreviewMouseDown;
        EditLayer.PreviewMouseMove += Eraser_PreviewMouseMove;
        EditLayer.PreviewMouseLeftButtonUp += Eraser_PreviewMouseUp;
        EditLayer.LostMouseCapture += (_, _) => FinishErasing();
        EditLayer.MouseLeave += (_, _) => HideEraserRing();
    }

    /// <summary>The eraser options row: its size, and a hint.</summary>
    private void BuildEraserOptions()
    {
        var range = PenSizeStep.RangeOf(ToolKind.Eraser)!.Value;
        _eraserOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _eraserOptionsPanel.Children.Add(Label(Loc.T("lbl.width")));
        _eraserWidthSlider = new Slider
        {
            Minimum = range.Min,
            Maximum = range.Max,
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center,
            Value = _eraserWidth,
        };
        _eraserWidthSlider.ValueChanged += (_, e) =>
        {
            _eraserWidth = e.NewValue;
            if (_eraserRing?.Visibility == Visibility.Visible) ShowEraserRing(_eraserRingAt);
        };
        _eraserOptionsPanel.Children.Add(_eraserWidthSlider);
        _eraserOptionsPanel.Children.Add(ValueReadout(_eraserWidthSlider, v => v.ToString("0")));
        _eraserOptionsPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("eraser.hint"),
            Foreground = Theme.ForegroundBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        ToolbarStack.Children.Add(_eraserOptionsPanel);
    }

    // ------------------------------------------------------------ dragging ---

    private void Eraser_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Editing || _tool != ToolKind.Eraser || _editingText != null) return;
        _erased = new List<ErasedItem>();
        _eraseLast = e.GetPosition(EditLayer);
        EraseAt(_eraseLast);
        EditLayer.CaptureMouse();
        e.Handled = true;
    }

    private void Eraser_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_phase != Phase.Editing || _tool != ToolKind.Eraser) return;
        var p = e.GetPosition(EditLayer);
        ShowEraserRing(p);
        if (_erased != null)
        {
            EraseAlong(_eraseLast, p);
            _eraseLast = p;
        }
        // No move hint: with the eraser a press erases, it never grabs.
        EditLayer.Cursor = null;
        e.Handled = true;
    }

    private void Eraser_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_erased == null) return;
        EraseAlong(_eraseLast, e.GetPosition(EditLayer));
        e.Handled = true;
        FinishErasing();
    }

    /// <summary>The drag is over (button up, or the capture was lost): record what it removed.</summary>
    private void FinishErasing()
    {
        if (_erased is not { } erased) return;
        _erased = null;   // before the release, whose LostMouseCapture comes back here
        if (EditLayer.IsMouseCaptured) EditLayer.ReleaseMouseCapture();
        if (erased.Count > 0) RecordErased(erased);
    }

    /// <summary>Erase at points along the segment, close enough together that a fast drag
    /// leaves no gaps.</summary>
    private void EraseAlong(Point from, Point to)
    {
        var d = to - from;
        double step = Math.Max(1, _eraserWidth / 4);
        int n = (int)Math.Ceiling(d.Length / step);
        if (n == 0) { EraseAt(to); return; }
        for (int i = 1; i <= n; i++) EraseAt(from + d * (i / (double)n));
    }

    private void EraseAt(Point p)
    {
        if (_erased is not { } erased) return;
        double radius = _eraserWidth / 2;

        foreach (var stroke in Ink.Strokes.HitTest(p, _eraserWidth).ToList())
        {
            int index = Ink.Strokes.IndexOf(stroke);
            if (index < 0) continue;
            Ink.Strokes.RemoveAt(index);
            erased.Add(new ErasedItem(null, null, stroke, index));
        }

        foreach (var host in AnnotationHosts())
        {
            for (int i = host.Children.Count - 1; i >= 0; i--)
            {
                if (host.Children[i] is not FrameworkElement el || !EraserTouches(el, p, radius)) continue;
                if (el == _selected) Deselect();
                host.Children.RemoveAt(i);
                erased.Add(new ErasedItem(host, el, null, i));
            }
        }
    }

    /// <summary>A line, strip or shape is touched on its outline (a filled shape anywhere on
    /// it); the other annotations within their bounds.</summary>
    private static bool EraserTouches(FrameworkElement el, Point p, double radius)
    {
        if (el is Shape shape && (shape is Path path ? path.Data : shape.RenderedGeometry) is { } geometry)
        {
            double x = shape is Path ? 0 : Canvas.GetLeft(shape), y = shape is Path ? 0 : Canvas.GetTop(shape);
            var local = new Point(p.X - (double.IsNaN(x) ? 0 : x), p.Y - (double.IsNaN(y) ? 0 : y));
            if (shape.RenderTransform is TranslateTransform tt) local.Offset(-tt.X, -tt.Y);

            var pen = new Pen(Brushes.Black, shape.StrokeThickness + 2 * radius);
            if (geometry.StrokeContains(pen, local)) return true;
            return shape is not Path && shape.Fill is SolidColorBrush { Color.A: > 0 } && geometry.FillContains(local);
        }

        var bounds = AnnotationBounds(el);
        bounds.Inflate(radius, radius);
        return bounds.Contains(p);
    }

    // ---------------------------------------------------------- right-click ---

    /// <summary>Right-click with the eraser: clear every stroke and annotation overlapping
    /// <paramref name="area"/> (region-local), as one undo step.</summary>
    private void EraseWithin(Rect area)
    {
        var erased = new List<ErasedItem>();
        for (int i = Ink.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = Ink.Strokes[i];
            if (!stroke.GetBounds().IntersectsWith(area)) continue;
            Ink.Strokes.RemoveAt(i);
            erased.Add(new ErasedItem(null, null, stroke, i));
        }
        foreach (var host in AnnotationHosts())
        {
            for (int i = host.Children.Count - 1; i >= 0; i--)
            {
                if (host.Children[i] is not FrameworkElement el || !AnnotationBounds(el).IntersectsWith(area)) continue;
                if (el == _selected) Deselect();
                host.Children.RemoveAt(i);
                erased.Add(new ErasedItem(host, el, null, i));
            }
        }
        if (erased.Count > 0) RecordErased(erased);
    }

    /// <summary>One undo step for everything erased; undo re-inserts in reverse order, so each
    /// item lands back at the index it was removed from.</summary>
    private void RecordErased(IReadOnlyList<ErasedItem> erased)
    {
        void Restore()
        {
            _suppressStrokeHistory = true;
            for (int i = erased.Count - 1; i >= 0; i--)
            {
                var item = erased[i];
                if (item.Stroke is { } stroke && !Ink.Strokes.Contains(stroke))
                    Ink.Strokes.Insert(Math.Min(item.Index, Ink.Strokes.Count), stroke);
                else if (item.Host is { } host && item.Element is { } el && !host.Children.Contains(el))
                    host.Children.Insert(Math.Min(item.Index, host.Children.Count), el);
            }
            _suppressStrokeHistory = false;
        }

        void Remove()
        {
            _suppressStrokeHistory = true;
            foreach (var item in erased)
            {
                if (item.Stroke is { } stroke) Ink.Strokes.Remove(stroke);
                else if (item.Host is { } host && item.Element is { } el) host.Children.Remove(el);
            }
            _suppressStrokeHistory = false;
        }

        _history.Push(undo: Restore, redo: Remove);
    }

    // ---------------------------------------------------------------- ring ---

    private void ShowEraserRing(Point at)
    {
        _eraserRingAt = at;
        if (_eraserRing == null)
        {
            _eraserRing = new Ellipse
            {
                Stroke = new SolidColorBrush(Theme.Accent),
                StrokeThickness = 1.5,
                Fill = EraserRingFill,
                IsHitTestVisible = false,
            };
            EditLayer.Children.Add(_eraserRing);   // above the hosts; chrome, not content
        }
        _eraserRing.Width = _eraserWidth;
        _eraserRing.Height = _eraserWidth;
        Canvas.SetLeft(_eraserRing, at.X - _eraserWidth / 2);
        Canvas.SetTop(_eraserRing, at.Y - _eraserWidth / 2);
        _eraserRing.Visibility = Visibility.Visible;
    }

    private void HideEraserRing()
    {
        if (_eraserRing != null) _eraserRing.Visibility = Visibility.Collapsed;
    }
}
