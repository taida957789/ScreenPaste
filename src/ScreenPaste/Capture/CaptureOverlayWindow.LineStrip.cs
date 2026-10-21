using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenPaste.Editor;

namespace ScreenPaste.Capture;

/// <summary>
/// The line tool (Ctrl+2). A drag draws one line, with an arrowhead at either end as the
/// toolbar says (Shift: at a multiple of 45 degrees). A click starts a line strip instead,
/// Snipaste-style: each further click adds a point, the last segment follows the pointer, and
/// right-click, Esc, Enter or a double-click finish it, as do switching tools and exporting.
/// Shift snaps each segment about its own start, and the arrowheads sit at the strip's two
/// ends only. The geometry lives in <see cref="PolylineBuilder"/> and <see cref="ArrowGeometry"/>.
///
/// While a strip is open, EditLayer's tunnelling events take every press and move inside the
/// region, so a click on an existing annotation adds a point rather than grabbing it.
/// </summary>
public partial class CaptureOverlayWindow
{
    private PolylineBuilder? _strip;      // the strip being clicked out
    private Path? _stripPath;             // its preview in ShapeHost, which becomes the annotation
    private Point _stripCursor;           // where its last segment points

    private void InitLineStrip()
    {
        EditLayer.PreviewMouseLeftButtonDown += LineStrip_PreviewMouseDown;
        EditLayer.PreviewMouseMove += LineStrip_PreviewMouseMove;
    }

    private static bool ShiftHeld() => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

    // ---------------------------------------------------------- single line ---

    private void Line_MouseDown(Point start)
    {
        _lineDragging = true;
        _lineStart = start;
        _linePreview = new Path
        {
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        StyleLine(_linePreview);
        ShapeHost.Children.Add(_linePreview);
        InteractionLayer.CaptureMouse();
    }

    private void Line_MouseMove(Point p)
    {
        if (_linePreview == null) return;
        if (ShiftHeld()) p = ArrowGeometry.SnapTo45(_lineStart, p);
        _linePreview.Data = ArrowGeometry.Build(_lineStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);
    }

    /// <summary>The press is over: a drag becomes a line, a click starts a strip there.</summary>
    private void Line_MouseUp(Point p)
    {
        _lineDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        var path = _linePreview;
        _linePreview = null;
        if (path == null) return;

        if (PolylineBuilder.IsClick(_lineStart, p))
        {
            _strip = PolylineBuilder.Start(_lineStart);
            _stripPath = path;
            UpdateStripPreview(_lineStart);
            return;
        }

        if (ShiftHeld()) p = ArrowGeometry.SnapTo45(_lineStart, p);
        path.Data = ArrowGeometry.Build(_lineStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);
        RecordAdded(ShapeHost, path);
    }

    /// <summary>The line tool's current colour and width (also applied to an open strip, so
    /// the wheel or the toolbar can still change it).</summary>
    private void StyleLine(Path path)
    {
        if (path.Stroke is not SolidColorBrush { Color: var c } || c != _lineColor)
        {
            var brush = new SolidColorBrush(_lineColor);
            path.Stroke = brush;
            path.Fill = brush;   // fills the arrowhead triangles
        }
        path.StrokeThickness = _lineWidth;
    }

    // ---------------------------------------------------------------- strip ---

    private void LineStrip_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_strip is not { } strip) return;
        e.Handled = true;
        // The first press of a double-click already added the point the second lands on.
        if (e.ClickCount >= 2)
        {
            FinishLineStrip();
            return;
        }
        var p = e.GetPosition(EditLayer);
        _strip = strip.Add(p, ShiftHeld());
        UpdateStripPreview(p);
    }

    private void LineStrip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_strip == null) return;
        UpdateStripPreview(e.GetPosition(EditLayer));
        e.Handled = true;   // no grab hint over annotations: a click adds a point
    }

    private void UpdateStripPreview(Point cursor)
    {
        if (_strip is not { } strip || _stripPath is not { } path) return;
        _stripCursor = cursor;
        StyleLine(path);
        path.Data = ArrowGeometry.BuildPolyline(strip.Preview(cursor, ShiftHeld()), _lineWidth, _lineArrowStart, _lineArrowEnd);
    }

    /// <summary>A key changed the line's width or arrowheads: show it on the open strip now.</summary>
    private void RefreshLineStrip()
    {
        if (_strip != null) UpdateStripPreview(_stripCursor);
    }

    /// <summary>End the open strip, if any: keep it as one undo step, or drop it when it is a
    /// single point or too small to see.</summary>
    private void FinishLineStrip()
    {
        if (_strip is not { } strip || _stripPath is not { } path) return;
        DiscardLineStrip();
        if (path.Parent != ShapeHost) return;   // already gone with its layer

        if (strip.Finish() is not { } points)
        {
            ShapeHost.Children.Remove(path);
            return;
        }
        StyleLine(path);
        path.Data = ArrowGeometry.BuildPolyline(points, _lineWidth, _lineArrowStart, _lineArrowEnd);
        RecordAdded(ShapeHost, path);
    }

    /// <summary>Forget the open strip; its preview goes with ShapeHost (back to framing).</summary>
    private void DiscardLineStrip()
    {
        _strip = null;
        _stripPath = null;
    }
}
