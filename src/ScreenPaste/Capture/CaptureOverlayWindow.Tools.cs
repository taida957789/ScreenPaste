using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Output;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The annotation tools' state, and Snipaste's keys for them. T / B / M / E and Ctrl+0..9 pick
/// a tool, Ctrl+Tab cycles through the toolbar, Tab flips the tool's variant (rectangle / rounded
/// / ellipse, line / arrow, Gaussian / mosaic), and the wheel, 1 / 2 or [ / ] step its width,
/// the font size for text (<see cref="ToolShortcuts"/>, <see cref="PenSizeStep"/>).
///
/// Shift squares a shape, blur or magnifier frame and makes a pen stroke a straight segment;
/// Shift+drag keeps an annotation move on one axis, the arrows move the selected annotation a
/// pixel, and Shift+wheel scales a selected rectangle or ellipse (<see cref="ShapeConstraints"/>).
/// Ctrl+Shift+Z clears everything as one undo step, Alt+C picks the colour under the cursor,
/// the wheel on a colour swatch steps the colour's opacity, and a middle-click in the text box
/// pastes the clipboard's text.
///
/// Split out of the main code-behind, which still draws with most tools (the line, number and
/// eraser tools and annotation copies have partials of their own); which key does what is
/// <see cref="OverlayKeyRouter"/>'s.
/// </summary>
public partial class CaptureOverlayWindow
{
    private const double MinShapeSize = 4;          // smaller drags are dropped; Shift+wheel stops here
    private const double ShapeScaleStep = 1.1;      // one Shift+wheel notch
    private const double MagnifyWheelZoomStep = 0.5;
    private static readonly TimeSpan LoupeFlashDuration = TimeSpan.FromMilliseconds(1200);

    private FrameworkElement? _nudged;              // the annotation the arrows are moving
    private Point _nudgeFrom;                       // its translate before the first press
    private Stroke? _straightStroke;                // a Shift pen stroke being dragged
    private DispatcherTimer? _loupeFlash;           // Alt+C: how long the loupe says "picked"

    private void InitTools()
    {
        Ink.PreviewMouseLeftButtonDown += Ink_PreviewMouseDown;
        Ink.PreviewMouseMove += Ink_PreviewMouseMove;
        Ink.PreviewMouseLeftButtonUp += Ink_PreviewMouseUp;
        Ink.LostMouseCapture += (_, _) => FinishStraightStroke();

        // The text box's own scroll viewer would swallow a bubbling wheel over it.
        PreviewMouseWheel += Overlay_PreviewTextWheel;
        // The arrows' moves end as an undo step before a click can start anything else.
        PreviewMouseDown += (_, _) => CommitAnnotationNudge();
        Deactivated += (_, _) => CommitAnnotationNudge();
        Closed += (_, _) => _loupeFlash?.Stop();

        // Registered before the main code-behind's EditLayer handlers, so each of these can
        // claim a press first: the eraser, an open line strip, a Ctrl+drag copy.
        InitNumber();
        InitEraser();
        InitLineStrip();
        InitClone();
    }

    // ---------------------------------------------------------- tool state ---

    private void SelectTool(ToolKind kind)
    {
        if (_editingText != null && kind != ToolKind.Text) CommitActiveText(discardIfEmpty: true);
        FinishLineStrip();
        _tool = kind;

        foreach (var b in _toolButtons)
        {
            bool active = (ToolKind)b.Tag! == kind;
            b.Background = active ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        }

        bool isNone = kind is ToolKind.None;
        bool isPen = kind is ToolKind.MarkerPen or ToolKind.Highlighter;
        bool isBlur = kind is ToolKind.Blur;
        bool isText = kind is ToolKind.Text;
        bool isShape = kind is ToolKind.Shape;
        bool isLine = kind is ToolKind.Line;
        bool isSticker = kind is ToolKind.Sticker;
        bool isMagnify = kind is ToolKind.Magnify;
        bool isNumber = kind is ToolKind.Number;
        bool isEraser = kind is ToolKind.Eraser;
        // Selection survives tool switches — grabbing works with every tool but the eraser.

        _penOptionsPanel.Visibility = isPen ? Visibility.Visible : Visibility.Collapsed;
        _blurOptionsPanel.Visibility = isBlur ? Visibility.Visible : Visibility.Collapsed;
        _textOptionsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
        _shapeOptionsPanel.Visibility = isShape ? Visibility.Visible : Visibility.Collapsed;
        _lineOptionsPanel.Visibility = isLine ? Visibility.Visible : Visibility.Collapsed;
        _stickerOptionsPanel.Visibility = isSticker ? Visibility.Visible : Visibility.Collapsed;
        _magnifyOptionsPanel.Visibility = isMagnify ? Visibility.Visible : Visibility.Collapsed;
        _numberOptionsPanel.Visibility = isNumber ? Visibility.Visible : Visibility.Collapsed;
        _eraserOptionsPanel.Visibility = isEraser ? Visibility.Visible : Visibility.Collapsed;
        if (!isEraser) HideEraserRing();

        Ink.EditingMode = isPen ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        Ink.IsHitTestVisible = isPen;
        // Region tools capture via InteractionLayer; with no tool it catches drags that
        // move the whole selection; sticker mode lets clicks reach the sticker images
        // below so they can be dragged/resized. (Annotation grabbing is handled earlier
        // by the tunneling EditLayer handlers, for every tool; so is the eraser's drag, which
        // needs the layer only so that a press on empty space reaches EditLayer.)
        InteractionLayer.IsHitTestVisible = isBlur || isText || isShape || isLine || isNone || isMagnify || isNumber || isEraser;
        InteractionLayer.Cursor = DefaultInteractionCursor();

        if (isPen) LoadPenControls();
        if (isBlur) SelectBlurKind(_blurKind);
        if (isShape) { SelectShapeKind(_shapeKind); SelectShapeStyle(_shapeFilled); }
        if (isLine) RefreshArrowToggles();
        if (isSticker && StickerHost.Children.Count == 0) AddSticker();
        if (isMagnify) { SelectMagnifyShape(_magnifyShape); RefreshMagnifyToggles(); }
        RefreshSwatchSelection();
    }

    /// <summary>With no tool, dragging the interaction layer moves the whole selection; every
    /// other tool draws with it (the eraser's ring sits on a cross). Hover feedback overrides
    /// this while something is grabbable.</summary>
    private Cursor DefaultInteractionCursor() => _tool switch
    {
        ToolKind.None => Cursors.SizeAll,
        ToolKind.Eraser => Cursors.Cross,
        _ => Cursors.Arrow,
    };

    private void SelectBlurKind(BlurKind kind)
    {
        _blurKind = kind;
        foreach (var b in _blurKindButtons)
        {
            bool active = (BlurKind)b.Tag! == kind;
            b.Background = active ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        }
    }

    private void LoadPenControls()
    {
        bool hl = _tool == ToolKind.Highlighter;
        _widthSlider.Value = hl ? _hlWidth : _penWidth;
        _opacitySlider.Value = hl ? _hlOpacity : _penOpacity;
        ApplyDrawingAttributes();
    }

    private void ApplyDrawingAttributes()
    {
        bool hl = _tool == ToolKind.Highlighter;
        double width = hl ? _hlWidth : _penWidth;
        double opacity = hl ? _hlOpacity : _penOpacity;
        Color baseColor = hl ? _hlColor : _penColor;
        var color = Color.FromArgb((byte)Math.Round(opacity * 255), baseColor.R, baseColor.G, baseColor.B);

        Ink.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = color,
            Width = width,
            Height = width,
            IsHighlighter = hl,
            FitToCurve = true,
            StylusTip = StylusTip.Ellipse,
        };
    }

    private void OnWidthChanged()
    {
        if (_tool == ToolKind.Highlighter) _hlWidth = _widthSlider.Value;
        else _penWidth = _widthSlider.Value;
        ApplyDrawingAttributes();
    }

    private void OnOpacityChanged()
    {
        if (_tool == ToolKind.Highlighter) _hlOpacity = _opacitySlider.Value;
        else _penOpacity = _opacitySlider.Value;
        ApplyDrawingAttributes();
    }

    private void OnColorPicked(Color c)
    {
        if (_tool == ToolKind.Text) { _textColor = Color.FromArgb(_textColor.A, c.R, c.G, c.B); ApplyTextStyle(); RefocusText(); }
        else if (_tool is ToolKind.Shape or ToolKind.Number) { _shapeColor = Color.FromArgb(_shapeColor.A, c.R, c.G, c.B); }
        else if (_tool == ToolKind.Line) { _lineColor = Color.FromArgb(_lineColor.A, c.R, c.G, c.B); }
        else if (_tool == ToolKind.Magnify) { _magnifyBorderColor = Color.FromArgb(_magnifyBorderColor.A, c.R, c.G, c.B); }
        else
        {
            if (_tool == ToolKind.Highlighter) _hlColor = c;
            else _penColor = c;
            ApplyDrawingAttributes();
        }
        RefreshSwatchSelection();
    }

    // ------------------------------------------------------- tool keys ---

    /// <summary>Execute an annotation tool command; false leaves the key unhandled.</summary>
    private bool RunToolCommand(OverlayRoute route, OverlayKeyContext ctx)
    {
        switch (route.Command)
        {
            case OverlayCommand.MoveAnnotation: NudgeSelectedAnnotation(ctx.Key); return true;
            case OverlayCommand.SelectTool: SwitchTool(route.Action?.Tool); return true;
            case OverlayCommand.NextTool: SwitchTool(ToolShortcuts.Cycle(ToolShortcuts.CycleOrder, _tool, 1)); return true;
            case OverlayCommand.PreviousTool: SwitchTool(ToolShortcuts.Cycle(ToolShortcuts.CycleOrder, _tool, -1)); return true;
            case OverlayCommand.ToggleToolVariant:
                if (!ctx.IsRepeat) ToggleToolVariant();   // a held Tab must not make it flicker
                return true;
            case OverlayCommand.PenSizeDown: StepPenSize(-1); return true;
            case OverlayCommand.PenSizeUp: StepPenSize(1); return true;
            case OverlayCommand.PickColor: PickScreenColor(); return true;
            case OverlayCommand.ClearAll: ClearAllAnnotations(); return true;
            case OverlayCommand.NumberDown: StepNextNumber(-1); return true;
            case OverlayCommand.NumberUp: StepNextNumber(1); return true;
            case OverlayCommand.CopyAnnotation: CopySelectedAnnotation(); return true;
            case OverlayCommand.PasteAnnotation: PasteCopiedAnnotation(); return true;
            case OverlayCommand.FinishLineStrip: FinishLineStrip(); return true;
            default: return false;   // None, PassThrough, and rows not wired up yet
        }
    }

    /// <summary>A tool key: pick the tool, unless the key has none or it is already active.</summary>
    private void SwitchTool(ToolKind? kind)
    {
        if (kind is { } k && k != _tool) SelectTool(k);
    }

    /// <summary>Tab: the shape's (or the magnifier frame's) kind, an arrow at the line's end,
    /// or Gaussian / mosaic, as the toolbar's own toggles would.</summary>
    private void ToggleToolVariant()
    {
        switch (_tool)
        {
            case ToolKind.Shape: SelectShapeKind(ToolShortcuts.NextShapeKind(_shapeKind)); break;
            case ToolKind.Line: _lineArrowEnd = !_lineArrowEnd; RefreshArrowToggles(); break;
            case ToolKind.Blur: SelectBlurKind(ToolShortcuts.NextBlurKind(_blurKind)); break;
            case ToolKind.Magnify: SelectMagnifyShape(ToolShortcuts.NextShapeKind(_magnifyShape)); break;
        }
        RefreshLineStrip();   // an open strip shows its new arrowhead at once
    }

    /// <summary>"Marker (B / Ctrl+3)" with the user's keys, plus the variant key's hint for a
    /// tool with variants.</summary>
    private string ToolTipFor(ToolKind kind)
    {
        var tip = WithHotkey(ToolLabel(kind), CaptureShortcuts.ForTool(kind) is { } id ? _shortcuts.Label(id) : null);
        if (ToolShortcuts.HasVariant(kind)) tip += " · " + WithHotkey(Loc.T("keys.toolVariant"), _shortcuts.Label(CaptureShortcuts.ToolVariant));
        if (kind == ToolKind.Magnify) tip += " - " + Loc.T("magnify.hint");
        if (kind == ToolKind.Line) tip += " - " + Loc.T("line.stripHint");
        if (kind == ToolKind.Number) tip += " - " + Loc.T("number.hint");
        return tip;
    }

    private static string ToolLabel(ToolKind kind) => kind switch
    {
        ToolKind.MarkerPen => Loc.T("tool.marker"),
        ToolKind.Highlighter => Loc.T("tool.highlighter"),
        ToolKind.Text => Loc.T("tool.text"),
        ToolKind.Shape => Loc.T("tool.shape"),
        ToolKind.Line => Loc.T("tool.line"),
        ToolKind.Sticker => Loc.T("tool.sticker"),
        ToolKind.Blur => Loc.T("tool.blur"),
        ToolKind.Magnify => Loc.T("tool.magnify"),
        ToolKind.Number => Loc.T("tool.number"),
        ToolKind.Eraser => Loc.T("tool.eraser"),
        _ => kind.ToString(),
    };

    // --------------------------------------------------- width and opacity ---

    /// <summary>The wheel, 1 / 2 or [ / ]: one step thinner or thicker. It goes through the
    /// tool's slider, whose ValueChanged updates the tool, and the size chip says the new
    /// value, since the toolbar may be hidden.</summary>
    private void StepPenSize(int direction)
    {
        if (WidthControl(_tool) is not { } control) return;   // a tool without a width
        control.Slider.Value = PenSizeStep.Next(_tool, control.Slider.Value, direction);
        FlashSizeNote($"{control.Label} {control.Slider.Value:0}");
        RefreshLineStrip();   // an open strip shows its new width at once
    }

    private (Slider Slider, string Label)? WidthControl(ToolKind tool) => tool switch
    {
        ToolKind.MarkerPen or ToolKind.Highlighter => (_widthSlider, Loc.T("lbl.width")),
        ToolKind.Shape => (_shapeWidthSlider, Loc.T("lbl.lineWidth")),
        ToolKind.Line => (_lineWidthSlider, Loc.T("lbl.lineWidth")),
        ToolKind.Text => (_textSizeSlider, Loc.T("lbl.size")),
        ToolKind.Blur => (_blurSlider, Loc.T("lbl.blurStrength")),
        ToolKind.Magnify => (_magnifyBorderSlider, Loc.T("lbl.border")),
        ToolKind.Number => (_numberSizeSlider, Loc.T("lbl.size")),
        ToolKind.Eraser => (_eraserWidthSlider, Loc.T("lbl.width")),
        _ => null,
    };

    /// <summary>
    /// The wheel while editing (from <see cref="OnDetectLevelWheel"/>): it re-zooms a selected
    /// magnifier; otherwise Shift+wheel scales a selected rectangle or ellipse and the wheel
    /// steps the tool's width. The toolbar keeps its own wheel (its swatches step opacity, the
    /// rest of it ignores the wheel, even with a magnifier selected), and a sticker under the
    /// sticker tool has already resized itself.
    /// </summary>
    private void OnEditingWheel(MouseWheelEventArgs e)
    {
        int direction = Math.Sign(e.Delta);
        if (direction == 0) return;
        if (e.OriginalSource is DependencyObject src && IsWithin(src, Toolbar)) return;
        if (WheelSelectedMagnifier(direction)) { e.Handled = true; return; }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Shift)
        {
            if (TryScaleSelectedShape(direction)) e.Handled = true;
            return;
        }
        if (modifiers != ModifierKeys.None || e.LeftButton == MouseButtonState.Pressed) return;
        StepPenSize(direction);
        e.Handled = true;
    }

    /// <summary>The wheel on a selected magnifier re-zooms it. True whenever one is selected,
    /// even at the end of its zoom range, so the wheel never falls through to the width.</summary>
    private bool WheelSelectedMagnifier(int direction)
    {
        if (_selected?.Parent != MagnifyHost || MagnifyEffects.SpecOf(_selected) is not { } spec) return false;
        if (MagnifyEffects.SetZoom(_selected, spec.Zoom + direction * MagnifyWheelZoomStep))
        {
            MagnifyEffects.ClampViewInto(_selected, RegionBounds());
            ResampleMagnifier(_selected);
            MagnifyHost.UpdateLayout();   // the view just resized; re-fit the marching box
            UpdateSelectionBox();
        }
        return true;
    }

    /// <summary>The wheel over the text being typed changes its font size.</summary>
    private void Overlay_PreviewTextWheel(object sender, MouseWheelEventArgs e)
    {
        if (_editingText is not { } box || e.OriginalSource is not DependencyObject src || !IsWithin(src, box)) return;
        if (Keyboard.Modifiers != ModifierKeys.None || e.Delta == 0) return;
        StepPenSize(Math.Sign(e.Delta));
        e.Handled = true;
    }

    /// <summary>The wheel on a colour swatch: the active tool's opacity in 5% steps, the pens
    /// through their opacity slider, the other tools in their colour's alpha.</summary>
    private void Swatch_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;   // never a width step as well
        int direction = Math.Sign(e.Delta);
        if (direction == 0) return;

        double opacity;
        switch (_tool)
        {
            case ToolKind.MarkerPen:
            case ToolKind.Highlighter:
                // OnOpacityChanged stores it for the pen and re-applies the drawing attributes.
                _opacitySlider.Value = PenSizeStep.NextOpacity(_opacitySlider.Value, direction);
                opacity = _opacitySlider.Value;
                break;
            case ToolKind.Text:
                _textColor = StepAlpha(_textColor, direction);
                ApplyTextStyle();
                opacity = _textColor.A / 255.0;
                break;
            case ToolKind.Shape:
            case ToolKind.Number: _shapeColor = StepAlpha(_shapeColor, direction); opacity = _shapeColor.A / 255.0; break;
            case ToolKind.Line: _lineColor = StepAlpha(_lineColor, direction); opacity = _lineColor.A / 255.0; break;
            case ToolKind.Magnify:
                _magnifyBorderColor = StepAlpha(_magnifyBorderColor, direction);
                opacity = _magnifyBorderColor.A / 255.0;
                break;
            default: return;
        }
        FlashSizeNote($"{Loc.T("lbl.opacity")} {opacity * 100:0}%");
    }

    private static Color StepAlpha(Color c, int direction) => Color.FromArgb(
        (byte)Math.Round(PenSizeStep.NextOpacity(c.A / 255.0, direction) * 255), c.R, c.G, c.B);

    /// <summary>Alt+C: the screen colour under the cursor becomes the tool's colour (keeping
    /// its opacity), and the loupe says so for a moment.</summary>
    private void PickScreenColor()
    {
        UpdateMagnifier(CursorLocal());   // sample exactly where the cursor is now
        OnColorPicked(_magColor);
        MagHint1.Text = Loc.T("mag.picked");
        _loupeFlash ??= new DispatcherTimer(LoupeFlashDuration, DispatcherPriority.Normal, (_, _) => EndLoupeFlash(), Dispatcher);
        _loupeFlash.Stop();   // restart: a second Alt+C keeps the note up for the full time
        _loupeFlash.Start();
    }

    private void EndLoupeFlash()
    {
        _loupeFlash?.Stop();
        MagHint1.Text = Loc.T("mag.copy");
    }

    // --------------------------------------------------------- constraints ---

    /// <summary>The rectangle a shape, blur or magnifier drag frames; Shift makes it a square.</summary>
    private static Rect DragRect(Point start, Point p) =>
        ShapeConstraints.Frame(start, p, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    /// <summary>The pointer's travel since <paramref name="grab"/>; Shift keeps a move on the
    /// axis it has travelled further along.</summary>
    private static (double Dx, double Dy) MoveDelta(Point grab, Point p)
    {
        double dx = p.X - grab.X, dy = p.Y - grab.Y;
        return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? AxisLock.Apply(dx, dy) : (dx, dy);
    }

    /// <summary>
    /// Shift held as a pen stroke starts: a straight segment from the press to the pointer
    /// instead of a freehand stroke. The highlighter (Snipaste's marker) also snaps to 45° while
    /// Shift is held. The stroke is on the canvas throughout, so the drag shows it exactly.
    /// </summary>
    private void Ink_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool is not (ToolKind.MarkerPen or ToolKind.Highlighter)) return;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;

        var p = e.GetPosition(Ink);
        var points = new StylusPointCollection { new StylusPoint(p.X, p.Y), new StylusPoint(p.X, p.Y) };
        _straightStroke = new Stroke(points, Ink.DefaultDrawingAttributes.Clone());
        Ink.Strokes.Add(_straightStroke);   // not a collected stroke: recorded when it is done
        Ink.CaptureMouse();
        e.Handled = true;   // InkCanvas must not collect a freehand stroke as well
    }

    private void Ink_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_straightStroke == null) return;
        MoveStraightStrokeEnd(e.GetPosition(Ink));
        e.Handled = true;
    }

    private void Ink_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_straightStroke == null) return;
        MoveStraightStrokeEnd(e.GetPosition(Ink));
        FinishStraightStroke();
        Ink.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void MoveStraightStrokeEnd(Point p)
    {
        if (_straightStroke is not { } stroke) return;
        var start = stroke.StylusPoints[0].ToPoint();
        if (_tool == ToolKind.Highlighter && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            p = ArrowGeometry.SnapTo45(start, p);
        stroke.StylusPoints[1] = new StylusPoint(p.X, p.Y);
    }

    /// <summary>The straight stroke is done (button up, or the capture was lost): record it.</summary>
    private void FinishStraightStroke()
    {
        if (_straightStroke is not { } stroke) return;
        _straightStroke = null;
        PushStrokeHistory(stroke);
    }

    /// <summary>Shift+wheel on a selected rectangle or ellipse: scale it about its centre, one
    /// undo step per notch. False when no such shape is selected.</summary>
    private bool TryScaleSelectedShape(int direction)
    {
        if (_selected is not Shape shape || shape.Parent != ShapeHost || shape is not (Rectangle or Ellipse)) return false;
        if (double.IsNaN(shape.Width) || double.IsNaN(shape.Height)) return true;

        double x = Canvas.GetLeft(shape), y = Canvas.GetTop(shape);
        var before = new Rect(double.IsNaN(x) ? 0 : x, double.IsNaN(y) ? 0 : y, shape.Width, shape.Height);
        var after = ShapeConstraints.ScaleAbout(before, direction > 0 ? ShapeScaleStep : 1 / ShapeScaleStep, MinShapeSize);
        if (after == before) return true;   // at its smallest: still the shape's wheel

        bool rounded = shape is Rectangle { RadiusX: > 0 };
        void Apply(Rect r)
        {
            LayoutShape(shape, r, rounded);
            ShapeHost.UpdateLayout();   // the selection box reads the arranged size
        }
        Apply(after);
        _history.Push(undo: () => Apply(before), redo: () => Apply(after));
        return true;
    }

    // ------------------------------------------------- annotation moves ---

    /// <summary>An arrow with an annotation selected: move it a pixel. The presses of a held
    /// arrow become one undo step (<see cref="CommitAnnotationNudge"/>).</summary>
    private void NudgeSelectedAnnotation(Key key)
    {
        if (_selected is not { } el) return;
        var (dx, dy) = CursorNudge.Direction(key);
        if (dx == 0 && dy == 0) return;

        if (_nudged != el)
        {
            CommitAnnotationNudge();
            var start = EnsureTranslate(el);
            _nudged = el;
            _nudgeFrom = new Point(start.X, start.Y);
        }
        var tt = EnsureTranslate(el);
        tt.X += dx;
        tt.Y += dy;
        AfterAnnotationMoved(el);
    }

    /// <summary>Record the arrows' moves since the first press, if any: on the arrow's key-up,
    /// before any other command or click, and when the overlay loses focus.</summary>
    private void CommitAnnotationNudge()
    {
        if (_nudged is not { } el) return;
        _nudged = null;
        PushMove(el, _nudgeFrom);
    }

    /// <summary>Record a move of <paramref name="el"/> from translate <paramref name="from"/> to
    /// where it is now; less than half a pixel is a click, not a move.</summary>
    private void PushMove(FrameworkElement el, Point from)
    {
        var tt = EnsureTranslate(el);
        double ox = from.X, oy = from.Y, nx = tt.X, ny = tt.Y;
        if (Math.Abs(nx - ox) < 0.5 && Math.Abs(ny - oy) < 0.5) return;

        _history.Push(
            undo: () => { var t = EnsureTranslate(el); t.X = ox; t.Y = oy; },
            redo: () => { var t = EnsureTranslate(el); t.X = nx; t.Y = ny; });
    }

    /// <summary>Catch up after an annotation moved (dragged or nudged).</summary>
    private void AfterAnnotationMoved(FrameworkElement el)
    {
        // A blur shows the pixels it currently sits over, so it has to follow the move.
        if (el.Parent == BlurHost) BlurEffects.Resample(el, BlurBeneath());
        // A magnifier keeps its content (it is tied to the framed source), but the frame
        // and connector are drawn relative to the view, so they get rebuilt.
        else if (el.Parent == MagnifyHost) MagnifyEffects.UpdateDecoration(el);
        UpdateSelectionBox();
    }

    // ------------------------------------------------------- clear, paste ---

    /// <summary>Ctrl+Shift+Z (or a right-click outside the region with the eraser): remove
    /// every annotation and ink stroke as a single undo step, so a slip costs one Ctrl+Z. Text
    /// being typed is finished first, and the number markers start again from 1.</summary>
    private void ClearAllAnnotations()
    {
        CommitActiveText(discardIfEmpty: true);
        if (!HasAnnotations()) return;
        Deselect();

        var layers = AnnotationHosts()
            .Select(host => (Host: host, Children: host.Children.Cast<UIElement>().ToArray()))
            .ToArray();
        var strokes = Ink.Strokes.ToArray();
        var numbers = _numbers;

        void Clear()
        {
            _suppressStrokeHistory = true;
            foreach (var (host, children) in layers)
                foreach (var child in children) host.Children.Remove(child);
            foreach (var stroke in strokes) Ink.Strokes.Remove(stroke);
            _suppressStrokeHistory = false;
            SetNumbers(NumberSequence.Start);
        }

        void Restore()
        {
            _suppressStrokeHistory = true;
            foreach (var (host, children) in layers)
                foreach (var child in children)
                    if (!host.Children.Contains(child)) host.Children.Add(child);
            foreach (var stroke in strokes)
                if (!Ink.Strokes.Contains(stroke)) Ink.Strokes.Add(stroke);
            _suppressStrokeHistory = false;
            SetNumbers(numbers);
        }

        Clear();
        _history.Push(undo: Restore, redo: Clear);
    }

    // ------------------------------------------------------------ shared helpers ---

    /// <summary>Record an annotation just added to <paramref name="host"/> as one undo step;
    /// redo puts it back at the same depth. <paramref name="undone"/> / <paramref name="redone"/>
    /// run once it is taken out / put back (the number marker's counter).</summary>
    private void RecordAdded(Canvas host, UIElement element, Action? undone = null, Action? redone = null)
    {
        int index = host.Children.IndexOf(element);
        _history.Push(
            undo: () =>
            {
                host.Children.Remove(element);
                undone?.Invoke();
            },
            redo: () =>
            {
                if (!host.Children.Contains(element))
                    host.Children.Insert(Math.Clamp(index, 0, host.Children.Count), element);
                redone?.Invoke();
            });
    }

    /// <summary>Every options row with colour swatches; the number marker's sets the shape colour.</summary>
    private StackPanel[] SwatchPanels() => new[]
    {
        _penOptionsPanel, _textOptionsPanel, _shapeOptionsPanel, _lineOptionsPanel, _magnifyOptionsPanel,
        _numberOptionsPanel,
    };

    /// <summary>A middle-click in the text being typed pastes the clipboard's text at the caret
    /// (and is never the middle-click output action).</summary>
    private void TextBox_PreviewMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || sender is not TextBox box || box.IsReadOnly) return;
        e.Handled = true;
        if (ClipboardService.GetText() is not { Length: > 0 } text) return;   // empty, or still locked

        box.SelectedText = text;
        box.CaretIndex = box.SelectionStart + box.SelectionLength;
        box.Focus();
    }
}
