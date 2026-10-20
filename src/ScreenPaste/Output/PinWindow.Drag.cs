using System.Windows;
using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using Forms = System.Windows.Forms;

namespace ScreenPaste.Output;

/// <summary>
/// Moving and resizing a pin with the left button (Snipaste). The drag is done by hand, from
/// the cursor's travel, rather than with DragMove, whose system move loop keeps every key from
/// the pin: so W/A/S/D move it a pixel at a time while the button is held (they move the
/// cursor, which the drag follows), Shift snaps its edges to other pins, windows and screens
/// (<see cref="MagneticSnap"/>), and the pins picked with it (Ctrl+click, Ctrl+A) move along.
/// A press on the border band scales the pin instead, keeping its shape
/// (<see cref="HandleGeometry.DragEdgesAspect"/>). Everything here is in physical screen px.
/// </summary>
public sealed partial class PinWindow
{
    private const double ResizeBandDip = 6;     // the border band that resizes, at 100% scaling
    private const double SnapDistanceDip = 10;  // how near an edge has to come to snap
    private const double MinResizePx = 8;       // a border drag keeps the shorter side this long
    private const double ClickSlopPx = 4;       // a press that moved the pin further was a drag, not a click

    /// <summary>A left drag moving pins: where the cursor and each moving window's top-left
    /// were when it began, and what Shift snaps to (found the first time Shift is held).</summary>
    private sealed record MoveDrag(Point StartCursor, Rect StartWindow,
        IReadOnlyList<(PinWindow Pin, Point TopLeft)> Moving, IReadOnlyList<Rect>? SnapTargets);

    /// <summary>A border drag scaling the pin from <see cref="StartState"/>.</summary>
    private sealed record BorderDrag(int Handle, Point StartCursor, Rect StartImage, PinTransformState StartState);

    private MoveDrag? _move;
    private BorderDrag? _resize;
    private bool _pressMoved;                   // the last press dragged the pin somewhere

    private void InitDrag()
    {
        MouseLeftButtonDown += OnLeftDown;
        MouseLeftButtonUp += (_, _) => EndLeftDrag();
        MouseMove += (_, _) => FollowCursor();
        LostMouseCapture += (_, _) =>
        {
            _move = null;
            _resize = null;
        };
    }

    /// <summary>
    /// A left press: a plain double-click closes the pin (when the settings say so), a Shift
    /// double-click toggles the thumbnail and a Ctrl+click picks the pin or drops it from the
    /// selection. Any other press drags the pin (a plain one on a pin not picked ends the
    /// selection), or resizes it on the border band. A press during a right-drag cancels it.
    /// WPF counts clicks by where they land in the window, which a dragged pin keeps under the
    /// cursor, so a quick press after a drag is a new press rather than a double-click.
    /// </summary>
    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        e.Handled = true;
        bool doubleClick = e.ClickCount == 2 && !_pressMoved;
        _pressMoved = false;
        if (CancelRightDrag()) return;

        var modifiers = Keyboard.Modifiers;
        if (doubleClick && modifiers == ModifierKeys.None && OnDoubleClick()) return;
        if (doubleClick && modifiers == ModifierKeys.Shift)
        {
            ToggleThumbnail();
            return;
        }
        if (modifiers == ModifierKeys.Control)
        {
            PinManager.ToggleSelected(this);
            return;
        }
        if (!PinManager.IsSelected(this)) PinManager.ClearSelection();
        if (BorderHandleAtCursor() is int handle) BeginResize(handle);
        else BeginMove();
    }

    /// <summary>The cursor moved, by the mouse or by W/A/S/D: carry on the drag under way,
    /// or show what a press would do here; the loupe follows either way.</summary>
    private void FollowCursor()
    {
        if (_move != null) ContinueMove();
        else if (_resize != null) ContinueResize();
        else if (_rightDragStart != null) ContinueRightDrag();
        else UpdateHoverCursor();
        UpdateLoupe();
    }

    private void EndLeftDrag()
    {
        if (_move == null && _resize == null) return;
        _move = null;
        _resize = null;
        ReleaseMouseCapture();
    }

    // -------------------------------------------------------------- move ---

    private void BeginMove()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        var moving = PinManager.GroupOf(this)
            .Select(pin => (Pin: pin, Window: pin.ScreenRect()))
            .Where(m => !m.Window.IsEmpty)
            .Select(m => (m.Pin, m.Window.TopLeft))
            .ToList();
        var window = ScreenRect();
        if (window.IsEmpty) return;
        _move = new MoveDrag(new Point(pt.X, pt.Y), window, moving, null);
        if (!CaptureMouse()) _move = null;
    }

    /// <summary>Every moving window goes as far as the cursor has; with Shift held, this
    /// pin's edges snap and the others keep their places beside it.</summary>
    private void ContinueMove()
    {
        if (_move is not { } move || !NativeMethods.GetCursorPos(out var pt)) return;
        var delta = new Point(pt.X, pt.Y) - move.StartCursor;
        if (Math.Abs(delta.X) >= ClickSlopPx || Math.Abs(delta.Y) >= ClickSlopPx) _pressMoved = true;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) delta = Snapped(move, delta);
        foreach (var (pin, topLeft) in move.Moving) pin.MoveWindowTo(topLeft + delta);
    }

    /// <summary>The drag's travel, changed so this pin's window lands on the nearest edges in reach.</summary>
    private Vector Snapped(MoveDrag move, Vector delta)
    {
        if (move.SnapTargets == null)
        {
            move = move with { SnapTargets = SnapTargets(move.Moving.Select(m => m.Pin).ToList()) };
            _move = move;
        }
        var moved = Rect.Offset(move.StartWindow, delta);
        var snapped = MagneticSnap.Snap(moved, move.SnapTargets!, SnapDistanceDip * _dpi);
        return delta + (snapped.TopLeft - moved.TopLeft);
    }

    /// <summary>What a Shift+drag snaps to: the other shown pins, the visible top-level
    /// windows (not the pins themselves, which may be moving) and each screen's edges and work area.</summary>
    private static IReadOnlyList<Rect> SnapTargets(IReadOnlyList<PinWindow> moving)
    {
        var targets = PinManager.VisiblePins()
            .Where(pin => !moving.Contains(pin))
            .Select(pin => pin.ScreenRect())
            .ToList();
        var pins = PinManager.Handles();
        foreach (var w in WindowEnumerator.Enumerate(IntPtr.Zero))
            if (!pins.Contains(w.Handle)) targets.Add(new Rect(w.Bounds.Left, w.Bounds.Top, w.Bounds.Width, w.Bounds.Height));
        foreach (var screen in Forms.Screen.AllScreens)
        {
            targets.Add(ToRect(screen.Bounds));
            targets.Add(ToRect(screen.WorkingArea));
        }
        return targets;
    }

    private static Rect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    /// <summary>Put the window's top-left (frame included) at <paramref name="topLeft"/>.</summary>
    private void MoveWindowTo(Point topLeft)
    {
        var hwnd = Handle;
        if (_closed || hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Move the window by (<paramref name="dx"/>, <paramref name="dy"/>) from where it is now.</summary>
    private void MoveWindowBy(int dx, int dy)
    {
        var hwnd = Handle;
        if (_closed || hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var r)) return;
        MoveWindowTo(new Point(r.Left + dx, r.Top + dy));
    }

    // ------------------------------------------------------------ resize ---

    /// <summary>The border handle under the cursor, or null: inside the band, a thumbnail
    /// (which has no border to drag) or a pin letting clicks through.</summary>
    private int? BorderHandleAtCursor()
    {
        if (_thumbnail != null || _clickThrough || !NativeMethods.GetCursorPos(out var pt)) return null;
        var window = ScreenRect();
        if (window.IsEmpty) return null;
        // A small pin keeps most of itself for dragging.
        double band = Math.Min(ResizeBandDip * _dpi, Math.Min(window.Width, window.Height) / 4);
        return HandleGeometry.BorderHandleAt(window, new Point(pt.X, pt.Y), band);
    }

    private void BeginResize(int handle)
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        _resize = new BorderDrag(handle, new Point(pt.X, pt.Y), ImageRect(), _state);
        if (!CaptureMouse()) _resize = null;
    }

    /// <summary>Scale the pin as its border is dragged: the new width as a zoom (within the
    /// zoom's range), the opposite edge or corner kept still.</summary>
    private void ContinueResize()
    {
        if (_resize is not { } r || !NativeMethods.GetCursorPos(out var pt)) return;
        double dx = pt.X - r.StartCursor.X, dy = pt.Y - r.StartCursor.Y;
        if (Math.Abs(dx) >= ClickSlopPx || Math.Abs(dy) >= ClickSlopPx) _pressMoved = true;
        var target = HandleGeometry.DragEdgesAspect(r.StartImage, r.Handle, dx, dy, MinResizePx);
        var next = r.StartState.WithZoom(r.StartState.Zoom * target.Width / r.StartImage.Width);
        var (w, h) = OutputSize(next);
        Present(next, HandleGeometry.ResizeAnchored(r.StartImage, r.Handle, w, h).TopLeft);
    }

    /// <summary>The resize arrow for the border handle under the cursor, else the usual one.</summary>
    private void UpdateHoverCursor()
    {
        Cursor = BorderHandleAtCursor() switch
        {
            0 or 7 => Cursors.SizeNWSE,
            2 or 5 => Cursors.SizeNESW,
            1 or 6 => Cursors.SizeNS,
            3 or 4 => Cursors.SizeWE,
            _ => null,
        };
    }
}
