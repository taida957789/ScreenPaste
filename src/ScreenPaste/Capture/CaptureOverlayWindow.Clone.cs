using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// Duplicating annotations, and marking areas with a right-click.
///
/// Ctrl+Shift+C copies the selected annotation and Ctrl+Shift+V pastes a copy of it, each
/// paste 10 px further on; Ctrl+drag on an annotation drags a copy off it and leaves the
/// original in place. Each copy is one undo step (<see cref="AnnotationCloner"/>).
///
/// A right-click with a fill tool (shape, blur, eraser) marks the UI element under the cursor,
/// cut to the region (<see cref="AreaMark"/>), and outside the region it marks the whole area:
/// a shape or blur over it, or the eraser clears it (<see cref="RightClickPolicy"/>).
/// </summary>
public partial class CaptureOverlayWindow
{
    private const double PasteStep = 10;             // px each paste lands further on
    private const double CloneDragThreshold = 3;     // px a Ctrl+press travels before it copies

    private FrameworkElement? _copiedAnnotation;     // detached copy taken by Ctrl+Shift+C
    private Canvas? _copiedHost;                     // the layer it came from
    private int _pasteCount;                         // pastes since that copy
    private FrameworkElement? _clonePending;         // Ctrl held on this annotation, not dragged yet
    private FrameworkElement? _cloneDrag;            // the copy a Ctrl+drag is moving
    private Point _cloneGrab;

    private void InitClone()
    {
        EditLayer.PreviewMouseLeftButtonDown += Clone_PreviewMouseDown;
        EditLayer.PreviewMouseMove += Clone_PreviewMouseMove;
        EditLayer.PreviewMouseLeftButtonUp += Clone_PreviewMouseUp;
        EditLayer.LostMouseCapture += (_, _) => EndCloneDrag();
    }

    // ------------------------------------------------------------- copy, paste ---

    /// <summary>Ctrl+Shift+C: remember a copy of the selected annotation as it is now.</summary>
    private void CopySelectedAnnotation()
    {
        if (_selected is not { Parent: Canvas host } el) return;
        if (AnnotationCloner.Clone(el, default, RegionBounds()) is not { } copy) return;
        _copiedAnnotation = copy;
        _copiedHost = host;
        _pasteCount = 0;
        FlashSizeNote(Loc.T("annot.copied"));
    }

    /// <summary>Ctrl+Shift+V: a copy of the copied annotation, a step further on each time,
    /// on top of its layer and selected.</summary>
    private void PasteCopiedAnnotation()
    {
        if (_copiedAnnotation is not { } template || _copiedHost is not { } host) return;
        var offset = new Vector(PasteStep, PasteStep) * (_pasteCount + 1);
        if (AnnotationCloner.Clone(template, offset, RegionBounds()) is not { } copy) return;
        _pasteCount++;

        InsertClone(host, copy, host.Children.Count);
        RecordAdded(host, copy);
        _selected = copy;
        UpdateSelectionBox();
    }

    /// <summary>Put a copy into its layer, wired and filled like the original.</summary>
    private void InsertClone(Canvas host, FrameworkElement copy, int index)
    {
        host.Children.Insert(Math.Clamp(index, 0, host.Children.Count), copy);
        if (copy is Image sticker) AttachStickerInteractions(sticker);
        host.UpdateLayout();   // the selection box and the samplers read the arranged size
        if (host == BlurHost) BlurEffects.Resample(copy, BlurBeneath());
        else if (host == MagnifyHost) ResampleMagnifier(copy);
    }

    // ---------------------------------------------------------------- Ctrl+drag ---

    private void Clone_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Editing || _editingText != null || _strip != null || _tool == ToolKind.Eraser) return;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        var p = e.GetPosition(EditLayer);
        if (HitAnnotation(p) is not { } hit) return;
        // Over a magnifier's framed source the press re-points that magnifier, as without Ctrl.
        if (hit.Parent != MagnifyHost && HitMagnifySource(p) != null) return;

        _clonePending = hit;
        _cloneGrab = p;
        _selected = hit;
        UpdateSelectionBox();
        EditLayer.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>Once the Ctrl+press really moves, the copy is made just above the original and
    /// handed to the ordinary annotation move, which drags it from here on.</summary>
    private void Clone_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_clonePending is not { } original) return;
        var p = e.GetPosition(EditLayer);
        if ((p - _cloneGrab).Length < CloneDragThreshold)
        {
            e.Handled = true;
            return;
        }

        _clonePending = null;
        if (original.Parent is not Canvas host || AnnotationCloner.Clone(original, default, RegionBounds()) is not { } copy)
        {
            EditLayer.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        InsertClone(host, copy, host.Children.IndexOf(original) + 1);
        _cloneDrag = copy;
        _selected = copy;
        var tt = EnsureTranslate(copy);
        _moveDragging = true;
        _moveGrab = _cloneGrab;
        _moveOrigX = tt.X;
        _moveOrigY = tt.Y;
        // Not handled: EditLayer_PreviewMouseMove moves the copy, Shift axis lock included.
    }

    private void Clone_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_clonePending == null && _cloneDrag == null) return;
        e.Handled = true;   // the copy is recorded below, not as a move
        EndCloneDrag();
    }

    /// <summary>The Ctrl+press is over (button up, or the capture was lost): a dragged copy is
    /// one undo step; a Ctrl+click without a drag only selected the annotation.</summary>
    private void EndCloneDrag()
    {
        var copy = _cloneDrag;
        if (copy == null && _clonePending == null) return;
        _clonePending = null;
        _cloneDrag = null;   // before the release, whose LostMouseCapture comes back here
        _moveDragging = false;
        if (EditLayer.IsMouseCaptured) EditLayer.ReleaseMouseCapture();
        if (copy?.Parent is Canvas host) RecordAdded(host, copy);
    }

    // ------------------------------------------------------------- area marks ---

    /// <summary>Right-click inside the region with a fill tool: mark the UI element under
    /// <paramref name="p"/> (RootCanvas px). With nothing detected there, the tool is finished,
    /// as any other tool's would be.</summary>
    private void MarkElementAt(Point p)
    {
        if (ElementRectAt(p) is { } area) MarkArea(area);
        else SelectTool(ToolKind.None);
    }

    /// <summary>The deepest detected UI element under <paramref name="p"/>, region-local and
    /// cut to the region.</summary>
    private Rect? ElementRectAt(Point p)
    {
        int sx = _vs.X + (int)Math.Round(p.X);
        int sy = _vs.Y + (int)Math.Round(p.Y);
        if (_elements == null || WindowEnumerator.HitTestWindow(_windows, sx, sy) is not { } window) return null;

        var region = new Rect(_vs.X + _selection.X, _vs.Y + _selection.Y, _selection.Width, _selection.Height);
        return AreaMark.ElementInRegion(_elements.CandidatesAt(window, sx, sy), region, MinShapeSize);
    }

    /// <summary>Right-click outside the region with a fill tool: mark all of it.</summary>
    private void MarkWholeArea()
    {
        if (_tool == ToolKind.Eraser) ClearAllAnnotations();
        else MarkArea(RegionBounds());
    }

    /// <summary>The fill tool's mark over <paramref name="area"/> (region-local), one undo
    /// step: a shape in the shape tool's style, a blur region, or the eraser clearing it.</summary>
    private void MarkArea(Rect area)
    {
        switch (_tool)
        {
            case ToolKind.Shape: RecordAdded(ShapeHost, CreateShape(area)); break;
            case ToolKind.Blur: RecordAdded(BlurHost, CreateBlur(area)); break;   // the record re-samples it
            case ToolKind.Eraser: EraseWithin(area); break;
        }
    }
}
