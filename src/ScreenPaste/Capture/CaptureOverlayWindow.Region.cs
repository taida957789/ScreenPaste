using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenPaste.Editor;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// Adjusting the chosen region while editing, and the right-click rule. The 8 handles
/// resize it (Ctrl: about the centre; Space held: move it instead) and dragging the interior
/// with no tool moves it (Shift: along one axis only). The arrows move it a pixel, Ctrl+arrows
/// grow and Shift+arrows shrink it. Annotations stay glued to the SCREENSHOT content: when
/// the region origin shifts by (dx,dy), every annotation element gets a compensating
/// translate and the ink strokes are transformed by the inverse.
///
/// A right-click undoes the innermost thing in progress (<see cref="RightClickPolicy"/>):
/// framing, it cancels the drag or leaves; editing, it finishes a line strip, ends the text,
/// lets a fill tool mark an area (CaptureOverlayWindow.Clone.cs), deselects, drops the tool
/// and finally discards the region to frame again (<see cref="ReturnToSelecting"/>).
///
/// Split out of the main code-behind. The geometry lives in <see cref="HandleGeometry"/>,
/// <see cref="RegionNudge"/> and <see cref="AxisLock"/>; which key does what is
/// <see cref="OverlayKeyRouter"/>'s.
/// </summary>
public partial class CaptureOverlayWindow
{
    // Selection-adjust state (editing phase): 8 resize handles + drag-to-move.
    // Handle indices: 0=NW 1=N 2=NE 3=W 4=E 5=SW 6=S 7=SE.
    private const double HandleSize = 12;
    private const int MinRegionSize = 20;
    private readonly List<Rectangle> _handles = new();
    private int _resizeHandle = -1;
    private bool _regionMoving;
    private Point _regionGrab;                // pointer at drag start (canvas px)
    private Int32Rect _regionStart;           // selection at drag start

    private void InitRegion()
    {
        // Bubbles up from everything on the canvas; while framing, RootCanvas holds the capture.
        RootCanvas.MouseRightButtonUp += RootCanvas_MouseRightButtonUp;
    }

    // ---------------------------------------------------- region adjust ---

    private void EnsureHandles()
    {
        if (_handles.Count > 0) return;
        Cursor[] cursors =
        {
            Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW,
            Cursors.SizeWE,                   Cursors.SizeWE,
            Cursors.SizeNESW, Cursors.SizeNS, Cursors.SizeNWSE,
        };
        for (int i = 0; i < 8; i++)
        {
            var h = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = new SolidColorBrush(Theme.Accent),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = cursors[i],
                Tag = i,
            };
            h.MouseLeftButtonDown += Handle_MouseDown;
            h.MouseMove += Handle_MouseMove;
            h.MouseLeftButtonUp += Handle_MouseUp;
            _handles.Add(h);
            HandleLayer.Children.Add(h);
        }
    }

    private void LayoutHandles()
    {
        var at = HandleGeometry.Place(
            new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height),
            HandleSize, outside: false);
        for (int i = 0; i < HandleGeometry.Count; i++)
        {
            Canvas.SetLeft(_handles[i], at[i].X);
            Canvas.SetTop(_handles[i], at[i].Y);
        }
    }

    private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle h || _phase != Phase.Editing) return;
        _resizeHandle = (int)h.Tag!;
        _regionGrab = e.GetPosition(RootCanvas);
        _regionStart = _selection;
        h.CaptureMouse();
        e.Handled = true;
    }

    private void Handle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_resizeHandle < 0) return;
        var p = e.GetPosition(RootCanvas);
        UpdateMagnifier(p);
        if (TryHoldMoveRegion(p)) return;   // Space held: move rather than resize

        var start = new Rect(_regionStart.X, _regionStart.Y, _regionStart.Width, _regionStart.Height);
        double dx = p.X - _regionGrab.X, dy = p.Y - _regionGrab.Y;
        // Ctrl resizes about the centre: the opposite edge mirrors the dragged one.
        var r = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            ? HandleGeometry.DragEdgesSymmetric(start, _resizeHandle, dx, dy, MinRegionSize, ScreenshotBounds())
            : HandleGeometry.DragEdges(start, _resizeHandle, dx, dy, MinRegionSize, ScreenshotBounds());
        ApplyRegion(Snap(r));
    }

    private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizeHandle < 0) return;
        _resizeHandle = -1;
        (sender as Rectangle)?.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Region_MoveStart(Point p)
    {
        _regionMoving = true;
        _regionGrab = p;
        _regionStart = _selection;
        InteractionLayer.CaptureMouse();
    }

    private void Region_MoveTo(Point p)
    {
        UpdateMagnifier(p);
        double dx = p.X - _regionGrab.X, dy = p.Y - _regionGrab.Y;
        // Shift keeps the move on one axis, the one the pointer has travelled further along.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) (dx, dy) = AxisLock.Apply(dx, dy);
        int x = Math.Clamp(_regionStart.X + (int)Math.Round(dx), 0, _screenshot.PixelWidth - _regionStart.Width);
        int y = Math.Clamp(_regionStart.Y + (int)Math.Round(dy), 0, _screenshot.PixelHeight - _regionStart.Height);
        ApplyRegion(new Int32Rect(x, y, _regionStart.Width, _regionStart.Height));
    }

    private void Region_MoveEnd()
    {
        _regionMoving = false;
        InteractionLayer.ReleaseMouseCapture();
    }

    /// <summary>Arrows: move, grow or shrink the region by a pixel. Like a mouse move, this
    /// is not recorded in the history.</summary>
    private void NudgeRegion(Key key, ModifierKeys modifiers)
    {
        var current = new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height);
        ApplyRegion(Snap(RegionNudge.Apply(current, key, modifiers, ScreenshotBounds(), MinRegionSize)));
    }

    /// <summary>Apply a new selection rect and re-layout everything that depends on it.</summary>
    private void ApplyRegion(Int32Rect r)
    {
        if (r == _selection) return;
        FinishLineStrip();   // its points are region-local: keep it where it was drawn

        int dx = r.X - _selection.X;
        int dy = r.Y - _selection.Y;
        if (dx != 0 || dy != 0) ShiftAnnotations(-dx, -dy);

        _selection = r;
        // A region RESIZE (as opposed to a move) leaves annotations where they are, so a
        // magnifier's framed source — stored in region-local coords — can end up outside the
        // new, smaller region. Pull it back in, or every later interaction with it would be
        // working from an out-of-bounds rect.
        ClampMagnifySourcesIntoRegion();

        var sel = new Rect(r.X, r.Y, r.Width, r.Height);
        ShowSelectionRect(sel);
        LayoutEditLayer(sel);
        LayoutHandles();
        PositionToolbar();
        InvalidateSampledLayers();   // the region now covers different screenshot pixels
        if (_selected != null) UpdateSelectionBox();
        else LayoutMagnifySourceHandles();
    }

    /// <summary>Keep annotations pinned to the screenshot content while the region moves.</summary>
    private void ShiftAnnotations(int dx, int dy)
    {
        foreach (var host in AnnotationHosts())
        {
            foreach (var child in host.Children)
            {
                if (child is not FrameworkElement el) continue;
                var tt = EnsureTranslate(el);
                tt.X += dx;
                tt.Y += dy;
                // A magnifier frames its source in region-local coords too, so that has to
                // travel along or the enlarged view would start showing a different area.
                MagnifyEffects.ShiftSource(el, dx, dy);
            }
        }
        if (Ink.Strokes.Count > 0)
            Ink.Strokes.Transform(new Matrix(1, 0, 0, 1, dx, dy), false);
    }

    /// <summary>Every layer that holds annotation elements (ink strokes live in Ink).</summary>
    private Canvas[] AnnotationHosts() => new[] { BlurHost, MagnifyHost, ShapeHost, StickerHost, TextHost };

    private bool HasAnnotations() =>
        Ink.Strokes.Count > 0 || AnnotationHosts().Any(h => h.Children.Count > 0);

    // ------------------------------------------------------------ right-click ---

    private void RootCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The toolbar keeps its own right-clicks (a custom swatch removes its colour), and
        // the text box being typed in keeps its context menu. The font list's popup routes
        // here through its logical parent, which IsWithin cannot follow, so an open list
        // is checked directly.
        if (_fontCombo?.IsDropDownOpen == true) return;
        if (e.OriginalSource is DependencyObject src &&
            (IsWithin(src, Toolbar) || (_editingText is { } box && IsWithin(src, box)))) return;

        var p = e.GetPosition(RootCanvas);
        var action = RightClickPolicy.Decide(new RightClickContext
        {
            Phase = _phase == Phase.Editing ? OverlayPhase.Editing : OverlayPhase.Selecting,
            Dragging = Mouse.LeftButton == MouseButtonState.Pressed,
            TextActive = _editingText != null,
            LineStripActive = _strip != null,
            HasSelection = _selected != null,
            Tool = _tool,
            HasAnnotations = HasAnnotations(),
            InsideRegion = new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height).Contains(p),
            AutoDetectAvailable = _elements != null,
        });
        if (RunRightClick(action, p)) e.Handled = true;
    }

    /// <summary>Execute a right-click decision made at <paramref name="p"/> (RootCanvas px);
    /// false leaves the click unhandled.</summary>
    private bool RunRightClick(RightClickAction action, Point p)
    {
        switch (action)
        {
            case RightClickAction.CancelDrag: CancelSelectingDrag(); return true;
            case RightClickAction.FinishLineStrip: FinishLineStrip(); return true;
            case RightClickAction.CommitText: CommitActiveText(discardIfEmpty: true); return true;
            case RightClickAction.Deselect: Deselect(); return true;
            case RightClickAction.AutoMarkElement: MarkElementAt(p); return true;
            case RightClickAction.MarkWholeArea: MarkWholeArea(); return true;
            case RightClickAction.DropTool: SelectTool(ToolKind.None); return true;
            case RightClickAction.ConfirmThenReturn:
                if (ConfirmDiscard(Loc.T("dlg.reselectTitle"), Loc.T("dlg.reselectMsg"), Loc.T("dlg.reselectYes")))
                    ReturnToSelecting();
                return true;
            case RightClickAction.ReturnToSelecting: ReturnToSelecting(); return true;
            case RightClickAction.Abort: Cancel(); return true;
            default: return false;
        }
    }

    /// <summary>
    /// Discard the region together with every annotation and their undo history, and go
    /// back to framing a new one, so the next right-click leaves. Only reached with no text
    /// box, drag or tool in progress.
    /// </summary>
    private void ReturnToSelecting()
    {
        Deselect();
        _editingText = null;   // its box goes with TextHost below
        DiscardLineStrip();    // and an open strip's preview with ShapeHost
        SetNumbers(NumberSequence.Start);
        Ink.Strokes.Clear();
        foreach (var host in AnnotationHosts()) host.Children.Clear();

        // Before the history clear: its Changed hook re-samples, which only editing does.
        _phase = Phase.Selecting;
        _history.Clear();

        EditLayer.Visibility = Visibility.Collapsed;
        HandleLayer.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        Cursor = Cursors.Cross;
        UpdateMask(null);
        Focus();   // a toolbar control that held the keyboard focus is gone

        var p = CursorLocal();
        UpdateMagnifier(p);
        UpdateDetect(p);
    }

    /// <summary>Ask before throwing work away, unless the user opted out; "don't ask again"
    /// is saved whatever the answer. True to go ahead. The defaults word it as leaving the
    /// capture.</summary>
    private bool ConfirmDiscard(string? title = null, string? message = null, string? confirm = null)
    {
        if (!_settings.ConfirmDiscardEdits) return true;

        var dlg = new ConfirmDiscardDialog(title, message, confirm) { Owner = this };
        bool? discard = dlg.ShowDialog();
        if (dlg.DontAskAgain)
        {
            _settings.ConfirmDiscardEdits = false;
            _settings.Save();
        }
        return discard == true;
    }
}
