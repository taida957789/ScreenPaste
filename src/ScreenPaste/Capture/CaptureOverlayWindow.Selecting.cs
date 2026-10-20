using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The selection phase: framing the capture region. Hovering highlights the UI element or
/// window under the cursor (Tab switches element / window detection, the wheel walks the
/// element levels) and a click takes it; a drag draws a rubber band (Shift: a square,
/// Space held: the band moves instead of growing; a right-click drops it, and with no drag a
/// right-click leaves). Space takes the highlighted area and
/// Ctrl+A the cursor's screen, then all screens. W/A/S/D and the arrows nudge the cursor a
/// pixel at a time, which any drag follows, and an Alt tap toggles crosshair guide lines.
///
/// Split out of the main code-behind, which owns the editing phase. The geometry lives in
/// <see cref="SelectionGeometry"/>, <see cref="DetectionChooser"/> and
/// <see cref="CursorNudge"/>; which key does what is <see cref="OverlayKeyRouter"/>'s.
/// </summary>
public partial class CaptureOverlayWindow
{
    // Selection-phase state
    private List<DetectedWindow> _windows = new();
    private ElementDetector? _elements;           // UIA element rects, scanned in background
    private DetectMode _detectMode = DetectMode.Element;   // Tab toggles
    private int _detectLevel;                     // 0 = deepest element … N = whole window
    private int _detectCandidates = 1;            // chain length at the current hover point
    private IntPtr _hoverWindow;
    private Point _dragStart;                     // the rubber band's anchor corner
    private Point _dragLast;                      // pointer at the previous drag move
    private bool _dragging;
    private bool _dragAborted;                    // a right-click dropped the drag; wait for the button up
    private Rect? _detectRect;

    // Key-driven state. _spaceHeld also turns a region-handle resize into a move.
    private const int MaxNudgeRepeat = 64;        // CursorNudge.Step has long since capped
    private bool _spaceHeld;                      // Space went down mid-drag and is still down
    private int _nudgeRepeat;                     // auto-repeats of the held nudge key
    private bool _guideLines;                     // AppSettings.SnipGuideLines

    private void InitSelecting()
    {
        _guideLines = _settings.SnipGuideLines;

        RootCanvas.MouseLeftButtonDown += RootCanvas_MouseDown;
        RootCanvas.MouseMove += RootCanvas_MouseMove;
        RootCanvas.MouseLeftButtonUp += RootCanvas_MouseUp;
        MouseWheel += OnDetectLevelWheel;
        // Capture lost mid-drag (Alt+Tab, another window taking it): the drag is over, so
        // the wheel works again and no later move picks up a stale Space hold.
        RootCanvas.LostMouseCapture += (_, _) => { _dragging = false; _spaceHeld = false; };
        // Space released over another window never reaches us; don't stay stuck moving.
        Deactivated += (_, _) => _spaceHeld = false;
    }

    private void RootCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        _dragStart = _dragLast = e.GetPosition(RootCanvas);
        _dragging = false;
        _dragAborted = false;
        RootCanvas.CaptureMouse();
    }

    private void RootCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        var p = e.GetPosition(RootCanvas);
        UpdateMagnifier(p);

        if (e.LeftButton == MouseButtonState.Pressed && !_dragAborted)
        {
            if (!_dragging && (Math.Abs(p.X - _dragStart.X) > 3 || Math.Abs(p.Y - _dragStart.Y) > 3))
                _dragging = true;

            if (_dragging)
            {
                DetectBorder.Visibility = Visibility.Collapsed;
                ShowSelectionRect(TrackDrag(p));
            }
            _dragLast = p;
        }
        else
        {
            UpdateDetect(p);
        }
    }

    /// <summary>The rubber band for pointer <paramref name="p"/>. Space held moves the anchor
    /// along with the pointer, so the band travels instead of growing; Shift squares it.</summary>
    private Rect TrackDrag(Point p)
    {
        var bounds = ScreenshotBounds();
        if (_spaceHeld) _dragStart = SelectionGeometry.Translate(_dragStart, p - _dragLast, bounds);
        return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            ? SelectionGeometry.Square(_dragStart, p, bounds)
            : MakeRect(_dragStart, p);
    }

    /// <summary>
    /// Hover auto-detect: snap to the deepest cached UI element under the cursor
    /// (Snipaste-style), falling back to the whole window until its UIA scan lands.
    /// <see cref="_detectLevel"/> (mouse wheel) walks outward toward the window, and window
    /// mode (Tab) always takes the window.
    /// </summary>
    private void UpdateDetect(Point p)
    {
        int sx = _vs.X + (int)Math.Round(p.X);
        int sy = _vs.Y + (int)Math.Round(p.Y);

        var win = WindowEnumerator.HitTestWindow(_windows, sx, sy);
        if (win is { } w)
        {
            if (w.Handle != _hoverWindow)
            {
                _hoverWindow = w.Handle;
                _detectLevel = 0;   // new window: restart at the deepest element
            }

            var windowRect = new Rect(w.Bounds.Left, w.Bounds.Top, w.Bounds.Width, w.Bounds.Height);
            var chain = _elements?.CandidatesAt(w, sx, sy) ?? new List<Rect> { windowRect };
            _detectCandidates = chain.Count;
            var chosen = DetectionChooser.Pick(chain, _detectMode, _detectLevel) ?? windowRect;
            _detectRect = new Rect(chosen.X - _vs.X, chosen.Y - _vs.Y, chosen.Width, chosen.Height);
        }
        else
        {
            _hoverWindow = IntPtr.Zero;
            _detectCandidates = 1;
            _detectRect = null;
        }

        if (_detectRect is { } dr)
        {
            Canvas.SetLeft(DetectBorder, dr.X);
            Canvas.SetTop(DetectBorder, dr.Y);
            DetectBorder.Width = dr.Width;
            DetectBorder.Height = dr.Height;
            DetectBorder.Visibility = Visibility.Visible;
            ShowSizeReadout(dr, Loc.T(_detectMode == DetectMode.Window ? "detect.window" : "detect.element"));
        }
        else
        {
            DetectBorder.Visibility = Visibility.Collapsed;
            SizeReadout.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Mouse wheel while framing: cycle the detection level (element ⇄ window),
    /// leaving window mode if Tab chose it. While editing it belongs to the annotations
    /// (<see cref="OnEditingWheel"/>).</summary>
    private void OnDetectLevelWheel(object sender, MouseWheelEventArgs e)
    {
        if (_phase == Phase.Editing)
        {
            OnEditingWheel(e);
            return;
        }
        if (_phase != Phase.Selecting || _dragging || _detectRect == null) return;
        (_detectMode, _detectLevel) = DetectionChooser.Wheel(_detectMode, _detectLevel, _detectCandidates, outward: e.Delta > 0);
        UpdateDetect(Mouse.GetPosition(RootCanvas));
        e.Handled = true;
    }

    /// <summary>
    /// Display geometry changed (resolution/monitor swap, e.g. an RDP resize). While still
    /// framing the shot, bail out — the frozen backdrop and coordinates no longer match the
    /// screen. Once editing, the annotations live on a fixed self-contained canvas, so the
    /// user's work is left intact.
    /// </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => { if (_phase == Phase.Selecting) Close(); });
    }

    private void RootCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        if (_dragAborted)
        {
            _dragAborted = false;   // the cancelled press ends here, taking nothing
            return;
        }

        // Read the drag before letting go of the capture: losing it ends the drag.
        Rect? finalRect = _dragging
            ? TrackDrag(e.GetPosition(RootCanvas))
            : _detectRect;                        // click on auto-detected window
        _dragging = false;
        RootCanvas.ReleaseMouseCapture();

        // Nothing selected, or a drag too small to be meant.
        if (finalRect is not { } r || r.Width < 4 || r.Height < 4) return;
        AcceptRegion(r);
    }

    /// <summary>
    /// Right-click mid-drag: drop the rubber band and go back to hovering. The press that
    /// started it is ignored until the button comes up, so neither moving on nor releasing it
    /// frames or takes anything.
    /// </summary>
    private void CancelSelectingDrag()
    {
        _dragAborted = true;
        _dragging = false;
        _spaceHeld = false;
        if (RootCanvas.IsMouseCaptured) RootCanvas.ReleaseMouseCapture();
        SelectionBorder.Visibility = Visibility.Collapsed;
        UpdateMask(null);
        UpdateDetect(CursorLocal());
    }

    /// <summary>Every way of choosing the area ends here: a click or drag, Space, Ctrl+A.
    /// A key can take the area with the button still down, so the capture goes too.</summary>
    private void AcceptRegion(Rect r)
    {
        if (RootCanvas.IsMouseCaptured) RootCanvas.ReleaseMouseCapture();
        _dragging = false;
        _spaceHeld = false;
        HideGuideLines();
        EnterEditing(r);
    }

    private void ShowSelectionRect(Rect r)
    {
        Canvas.SetLeft(SelectionBorder, r.X);
        Canvas.SetTop(SelectionBorder, r.Y);
        SelectionBorder.Width = r.Width;
        SelectionBorder.Height = r.Height;
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateMask(r);
        ShowSizeReadout(r);
    }

    /// <summary>The "W × H" chip above <paramref name="r"/>; <paramref name="note"/> (the
    /// detection mode while hovering) follows the size.</summary>
    private void ShowSizeReadout(Rect r, string? note = null)
    {
        string size = $"{(int)r.Width} × {(int)r.Height}";
        SizeText.Text = note == null ? size : $"{size} · {note}";
        double chipH = 30 * _dpiScale;   // physical footprint (chip is counter-scaled)
        double x = r.X;
        double y = r.Y - chipH;
        if (y < 2) y = r.Y + 4;
        Canvas.SetLeft(SizeReadout, Math.Round(x));
        Canvas.SetTop(SizeReadout, Math.Round(y));
        SizeReadout.Visibility = Visibility.Visible;
    }

    private void UpdateMask(Rect? selection)
    {
        var outer = new RectangleGeometry(new Rect(0, 0, _vs.Width, _vs.Height));
        if (selection is { } s)
        {
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(outer);
            group.Children.Add(new RectangleGeometry(s));
            MaskPath.Data = group;
        }
        else
        {
            MaskPath.Data = outer;
        }
    }

    // ------------------------------------------------------------ keyboard ---
    // Commands routed by OverlayKeyRouter (dispatch: CaptureOverlayWindow.Keyboard.cs).

    /// <summary>Tab: UI element ⇄ window detection, applied to the current hover.</summary>
    private void ToggleDetectMode()
    {
        _detectMode = DetectionChooser.Toggle(_detectMode);
        UpdateDetect(CursorLocal());
    }

    /// <summary>Space: take the highlighted window or element.</summary>
    private void AcceptDetected()
    {
        if (_detectRect is { } dr) AcceptRegion(dr);
    }

    /// <summary>Ctrl+A: the cursor's monitor, then all screens, then the monitor again.
    /// While editing the region is re-applied, so the annotations keep their content.</summary>
    private void CycleScreenArea()
    {
        var bounds = ScreenshotBounds();
        var monitors = MonitorRects.Get(_vs)
            .Select(m => Rect.Intersect(m, bounds))
            .Where(m => m.Width > 0 && m.Height > 0)
            .ToList();
        var monitor = SelectionGeometry.MonitorAt(monitors, CursorLocal(), bounds);

        Rect? current = _phase == Phase.Editing
            ? new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height)
            : null;
        var next = SelectionGeometry.NextScreenRect(current, monitor, bounds, monitors.Count);

        if (_phase == Phase.Selecting) AcceptRegion(next);
        else ApplyRegion(Snap(next));
    }

    /// <summary>
    /// W/A/S/D (and the arrows while framing): move the OS cursor, faster while the key is
    /// held. The move arrives as WM_MOUSEMOVE too, which every drag follows (rubber band,
    /// region handles and move, drawing), so the left button held gives pixel-exact drags.
    /// The hover is refreshed straight away rather than a message later.
    /// </summary>
    private void NudgeCursor(Key key, bool isRepeat)
    {
        _nudgeRepeat = isRepeat ? Math.Min(_nudgeRepeat + 1, MaxNudgeRepeat) : 0;
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        var (dx, dy) = CursorNudge.Direction(key);
        int step = CursorNudge.Step(_nudgeRepeat);
        var (x, y) = CursorNudge.Clamp(pt.X + dx * step, pt.Y + dy * step, _vs);
        if ((x == pt.X && y == pt.Y) || !NativeMethods.SetCursorPos(x, y)) return;

        if (_phase == Phase.Selecting && Mouse.LeftButton == MouseButtonState.Released)
        {
            var local = _vs.ToLocal(x, y);
            UpdateMagnifier(local);
            UpdateDetect(local);
        }
    }

    /// <summary>Space down mid-drag: the area moves with the pointer until its key-up.</summary>
    private void BeginHoldToMove() => _spaceHeld = true;

    /// <summary>Space released: drags go back to resizing.</summary>
    private void EndHoldToMove() => _spaceHeld = false;

    /// <summary>
    /// Space held while dragging a region handle (editing): the region moves with the
    /// pointer instead, kept on the screenshot, and resizing resumes from there once Space
    /// is released. False when Space is not held.
    /// </summary>
    private bool TryHoldMoveRegion(Point p)
    {
        if (!_spaceHeld) return false;
        var current = new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height);
        var moved = Snap(HandleGeometry.ClampInto(
            Rect.Offset(current, p - _regionGrab), ScreenshotBounds(), MinRegionSize));
        _regionStart = moved;   // the resize base, should Space be released mid-drag
        _regionGrab = p;
        ApplyRegion(moved);
        return true;
    }

    /// <summary>Alt tap: toggle the guide lines (remembered), showing the loupe if hidden.</summary>
    private void ToggleGuideLines()
    {
        _guideLines = !_guideLines;
        _settings.SnipGuideLines = _guideLines;
        _settings.Save();
        UpdateMagnifier(CursorLocal());   // places (or hides) the lines with the loupe
    }

    /// <summary>From <see cref="UpdateMagnifier"/>: the lines cross at the hovered pixel,
    /// and only while framing.</summary>
    private void UpdateGuideLines(int px, int py)
    {
        if (!_guideLines || _phase != Phase.Selecting)
        {
            HideGuideLines();
            return;
        }
        Canvas.SetLeft(GuideH, 0);
        Canvas.SetTop(GuideH, py);
        GuideH.Width = _vs.Width;
        Canvas.SetLeft(GuideV, px);
        Canvas.SetTop(GuideV, 0);
        GuideV.Height = _vs.Height;
        GuideH.Visibility = Visibility.Visible;
        GuideV.Visibility = Visibility.Visible;
    }

    private void HideGuideLines()
    {
        GuideH.Visibility = Visibility.Collapsed;
        GuideV.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>The whole screenshot in overlay-local physical px.</summary>
    private Rect ScreenshotBounds() => new(0, 0, _screenshot.PixelWidth, _screenshot.PixelHeight);

    /// <summary>The cursor in RootCanvas space (overlay-local physical px).</summary>
    private Point CursorLocal() =>
        NativeMethods.GetCursorPos(out var pt) ? _vs.ToLocal(pt.X, pt.Y) : Mouse.GetPosition(RootCanvas);

    private static Int32Rect Snap(Rect r) => new(
        (int)Math.Round(r.X), (int)Math.Round(r.Y),
        (int)Math.Round(r.Width), (int)Math.Round(r.Height));
}
