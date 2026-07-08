using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using ScreenPaste.Output;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

public partial class CaptureOverlayWindow : Window
{
    private enum Phase { Selecting, Editing }

    private readonly BitmapSource _screenshot;
    private readonly VirtualScreen _vs;
    private readonly AppSettings _settings;

    private double _dpiScale = 1.0;
    private Phase _phase = Phase.Selecting;

    // Selection-phase state
    private List<DetectedWindow> _windows = new();
    private Point _dragStart;
    private bool _dragging;
    private Rect? _detectRect;
    private Int32Rect _selection;                 // physical px, screenshot/local coords

    // Editing state
    private ToolKind _tool = ToolKind.MarkerPen;
    private BlurKind _blurKind = BlurKind.Gaussian;
    private readonly EditHistory _history = new();
    private bool _suppressStrokeHistory;

    // Configurable editor shortcuts (from settings.json).
    private KeyGesture? _undoGesture, _redoGesture, _copyGesture, _saveGesture, _quickSaveGesture;

    // Blur-drag state
    private bool _blurDragging;
    private Point _blurStart;
    private Rectangle? _blurPreview;

    // Per-tool brush settings (loaded from AppSettings, saved back on close)
    private double _penWidth, _penOpacity, _hlWidth, _hlOpacity, _blurStrength;
    private Color _penColor, _hlColor;

    // Text-tool settings + state
    private string _textFont = "Segoe UI";
    private double _textSize = 24;
    private Color _textColor;
    private bool _textBold, _textItalic, _textStrike;
    private TextBox? _editingText;

    // Shape-tool settings + state
    private ShapeKind _shapeKind = ShapeKind.Rectangle;
    private bool _shapeFilled;
    private Color _shapeColor;      // ARGB (alpha = opacity)
    private double _shapeWidth = 3; // outline thickness
    private bool _shapeDragging;
    private Point _shapeStart;
    private Shape? _shapePreview;

    // Line-tool settings + state (each end can be an arrowhead)
    private Color _lineColor;
    private double _lineWidth = 3;
    private bool _lineArrowStart, _lineArrowEnd;
    private bool _lineDragging;
    private Point _lineStart;
    private Path? _linePreview;

    // Sticker (pasted image) drag state
    private Image? _stickerDrag;
    private Point _stickerGrab;

    // Select-tool state (click annotations to move them, Delete to remove)
    private FrameworkElement? _selected;
    private Rectangle? _selectionBox;
    private bool _moveDragging;
    private Point _moveGrab;                  // pointer position at drag start
    private double _moveOrigX, _moveOrigY;    // element translate at drag start

    // Toolbar controls we need to read/update
    private Slider _widthSlider = null!, _opacitySlider = null!, _blurSlider = null!, _textSizeSlider = null!, _shapeWidthSlider = null!, _lineWidthSlider = null!;
    private StackPanel _blurOptionsPanel = null!, _penOptionsPanel = null!, _textOptionsPanel = null!, _shapeOptionsPanel = null!, _lineOptionsPanel = null!, _stickerOptionsPanel = null!;
    private Button _arrowStartButton = null!, _arrowEndButton = null!;
    private ComboBox _fontCombo = null!;
    private Button _boldButton = null!, _italicButton = null!, _strikeButton = null!;
    private Button _undoButton = null!, _redoButton = null!;
    private readonly List<Button> _toolButtons = new();
    private readonly List<Button> _blurKindButtons = new();
    private readonly List<Button> _shapeKindButtons = new();
    private readonly List<Button> _shapeStyleButtons = new();

    public CaptureOverlayWindow(BitmapSource screenshot, VirtualScreen vs, AppSettings settings)
    {
        _screenshot = screenshot;
        _vs = vs;
        _settings = settings;

        InitializeComponent();

        _penWidth = settings.PenWidth;
        _penOpacity = settings.PenOpacity;
        _penColor = ParseColor(settings.PenColor, Colors.Red);
        _hlWidth = settings.HighlighterWidth;
        _hlOpacity = settings.HighlighterOpacity;
        _hlColor = ParseColor(settings.HighlighterColor, Colors.Yellow);
        _blurStrength = settings.GaussianStrength;
        _textFont = settings.TextFont;
        _textSize = settings.TextSize;
        _textColor = ParseColor(settings.TextColor, Colors.Red);
        _textBold = settings.TextBold;
        _textItalic = settings.TextItalic;
        _textStrike = settings.TextStrikethrough;
        _shapeKind = Enum.TryParse<ShapeKind>(settings.ShapeKind, out var sk) ? sk : ShapeKind.Rectangle;
        _shapeFilled = settings.ShapeFilled;
        _shapeColor = ParseColor(settings.ShapeColor, Colors.Red);
        _shapeWidth = settings.ShapeWidth;
        _lineColor = ParseColor(settings.LineColor, Colors.Red);
        _lineWidth = settings.LineWidth;
        _lineArrowStart = settings.LineArrowStart;
        _lineArrowEnd = settings.LineArrowEnd;

        _undoGesture = HotkeyGesture.Parse(settings.UndoHotkey);
        _redoGesture = HotkeyGesture.Parse(settings.RedoHotkey);
        _copyGesture = HotkeyGesture.Parse(settings.CopyHotkey);
        _saveGesture = HotkeyGesture.Parse(settings.SaveHotkey);
        _quickSaveGesture = HotkeyGesture.Parse(settings.QuickSaveHotkey);

        ScreenImage.Source = _screenshot;
        ScreenImage.Width = vs.Width;
        ScreenImage.Height = vs.Height;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { Activate(); Focus(); };

        RootCanvas.MouseLeftButtonDown += RootCanvas_MouseDown;
        RootCanvas.MouseMove += RootCanvas_MouseMove;
        RootCanvas.MouseLeftButtonUp += RootCanvas_MouseUp;
        KeyDown += OnKeyDown;

        // Tunneling handlers run before InkCanvas / InteractionLayer see the click:
        // direct-manipulation grab of annotations with ANY tool, and commit-on-click-
        // outside for the text box.
        PreviewMouseLeftButtonDown += Window_PreviewMouseDown;
        EditLayer.PreviewMouseLeftButtonDown += EditLayer_PreviewMouseDown;
        EditLayer.PreviewMouseMove += EditLayer_PreviewMouseMove;
        EditLayer.PreviewMouseLeftButtonUp += EditLayer_PreviewMouseUp;

        Ink.StrokeCollected += Ink_StrokeCollected;
        InteractionLayer.MouseLeftButtonDown += Blur_MouseDown;
        InteractionLayer.MouseMove += Blur_MouseMove;
        InteractionLayer.MouseLeftButtonUp += Blur_MouseUp;

        // Drag the toolbar by its empty chrome (buttons/sliders handle their own clicks).
        Toolbar.Cursor = Cursors.SizeAll;
        Toolbar.MouseLeftButtonDown += Toolbar_DragStart;
        Toolbar.MouseMove += Toolbar_DragMove;
        Toolbar.MouseLeftButtonUp += Toolbar_DragEnd;

        _history.Changed += UpdateHistoryButtons;

        UpdateMask(null);
    }

    // ---------------------------------------------------------------- setup ---

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        _dpiScale = dpi <= 0 ? 1.0 : dpi / 96.0;

        // Author children in physical px; compensate for WPF's per-monitor scaling.
        RootCanvas.RenderTransform = new ScaleTransform(1.0 / _dpiScale, 1.0 / _dpiScale);

        // The toolbar is UI chrome, not screenshot content: counter-scale it so its net
        // transform is identity — otherwise its text renders at a fractional scale on
        // high-DPI monitors (blurry ClearType, undersized). Display mode keeps the
        // small labels pixel-snapped.
        Toolbar.RenderTransform = new ScaleTransform(_dpiScale, _dpiScale);
        Toolbar.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(Toolbar, TextFormattingMode.Display);

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            _vs.X, _vs.Y, _vs.Width, _vs.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        // Snapshot windows for auto-detect (exclude our own overlay).
        _windows = WindowEnumerator.Enumerate(hwnd);
    }

    // ------------------------------------------------------- selection phase ---

    private void RootCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        _dragStart = e.GetPosition(RootCanvas);
        _dragging = false;
        RootCanvas.CaptureMouse();
    }

    private void RootCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        var p = e.GetPosition(RootCanvas);

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_dragging && (Math.Abs(p.X - _dragStart.X) > 3 || Math.Abs(p.Y - _dragStart.Y) > 3))
                _dragging = true;

            if (_dragging)
            {
                DetectBorder.Visibility = Visibility.Collapsed;
                var r = MakeRect(_dragStart, p);
                ShowSelectionRect(r);
            }
        }
        else
        {
            // Hover: auto-detect the window under the cursor.
            int sx = _vs.X + (int)Math.Round(p.X);
            int sy = _vs.Y + (int)Math.Round(p.Y);
            _detectRect = WindowEnumerator.HitTest(_windows, _vs, sx, sy);
            if (_detectRect is { } dr)
            {
                Canvas.SetLeft(DetectBorder, dr.X);
                Canvas.SetTop(DetectBorder, dr.Y);
                DetectBorder.Width = dr.Width;
                DetectBorder.Height = dr.Height;
                DetectBorder.Visibility = Visibility.Visible;
                ShowSizeReadout(dr);
            }
            else
            {
                DetectBorder.Visibility = Visibility.Collapsed;
                SizeReadout.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RootCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Selecting) return;
        RootCanvas.ReleaseMouseCapture();

        Rect finalRect;
        if (_dragging)
        {
            finalRect = MakeRect(_dragStart, e.GetPosition(RootCanvas));
        }
        else if (_detectRect is { } dr)
        {
            finalRect = dr;                       // click on auto-detected window
        }
        else
        {
            return;                               // nothing selected
        }

        if (finalRect.Width < 4 || finalRect.Height < 4) return;
        EnterEditing(finalRect, e.GetPosition(RootCanvas));
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

    private void ShowSizeReadout(Rect r)
    {
        SizeText.Text = $"{(int)r.Width} × {(int)r.Height}";
        double x = r.X;
        double y = r.Y - 24;
        if (y < 2) y = r.Y + 4;
        Canvas.SetLeft(SizeReadout, x);
        Canvas.SetTop(SizeReadout, y);
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

    private static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // ---------------------------------------------------------- edit phase ---

    private void EnterEditing(Rect r, Point cursor)
    {
        _phase = Phase.Editing;
        Cursor = Cursors.Arrow;

        _selection = new Int32Rect(
            (int)Math.Round(r.X), (int)Math.Round(r.Y),
            (int)Math.Round(r.Width), (int)Math.Round(r.Height));

        // Clamp to screenshot bounds.
        _selection.X = Math.Clamp(_selection.X, 0, _screenshot.PixelWidth - 1);
        _selection.Y = Math.Clamp(_selection.Y, 0, _screenshot.PixelHeight - 1);
        _selection.Width = Math.Clamp(_selection.Width, 1, _screenshot.PixelWidth - _selection.X);
        _selection.Height = Math.Clamp(_selection.Height, 1, _screenshot.PixelHeight - _selection.Y);

        var sel = new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height);
        ShowSelectionRect(sel);
        DetectBorder.Visibility = Visibility.Collapsed;

        Canvas.SetLeft(EditLayer, sel.X);
        Canvas.SetTop(EditLayer, sel.Y);
        EditLayer.Width = sel.Width;
        EditLayer.Height = sel.Height;
        Ink.Width = sel.Width;
        Ink.Height = sel.Height;
        InteractionLayer.Width = sel.Width;
        InteractionLayer.Height = sel.Height;
        EditLayer.Visibility = Visibility.Visible;

        BuildToolbar();
        SelectTool(ToolKind.MarkerPen);
        Toolbar.Visibility = Visibility.Visible;   // must be visible before measuring
        PositionToolbar(cursor);
        UpdateHistoryButtons();
    }

    // -------------------------------------------------------------- toolbar ---

    // Segoe MDL2 Assets glyphs (built into Win10/11) — icon-only toolbar.
    private static string Glyph(int code) => char.ConvertFromUtf32(code);
    private static readonly string GlyphMarker = Glyph(0xE70F);      // Edit (pen)
    private static readonly string GlyphHighlighter = Glyph(0xE891); // Highlight
    private static readonly string GlyphText = Glyph(0xE8D2);        // Font (A)
    private static readonly string GlyphShape = Glyph(0xE71A);       // Stop (square/block)
    private static readonly string GlyphLine = Glyph(0xE72A);        // Forward (arrow) — line/arrow tool
    private static readonly string GlyphSticker = Glyph(0xE8B9);     // Pictures (paste image)
    private static readonly string GlyphBlur = Glyph(0xE80A);        // GridView (mosaic look)
    private static readonly string GlyphUndo = Glyph(0xE7A7);        // Undo
    private static readonly string GlyphRedo = Glyph(0xE7A6);        // Redo
    private static readonly string GlyphCopy = Glyph(0xE8C8);        // Copy
    private static readonly string GlyphSave = Glyph(0xE74E);        // Save
    private static readonly string GlyphPin = Glyph(0xE840);         // Pin
    private static readonly string GlyphClose = Glyph(0xE711);       // Cancel

    private void BuildToolbar()
    {
        ToolbarStack.Children.Clear();
        _toolButtons.Clear();
        _blurKindButtons.Clear();
        _shapeKindButtons.Clear();
        _shapeStyleButtons.Clear();

        Toolbar.Background = Theme.PanelBrush;
        Toolbar.BorderBrush = Theme.ButtonBorderBrush;

        var toolsRow = new StackPanel { Orientation = Orientation.Horizontal };
        toolsRow.Children.Add(MakeToolButton(GlyphMarker, Loc.T("tool.marker"), ToolKind.MarkerPen));
        toolsRow.Children.Add(MakeToolButton(GlyphHighlighter, Loc.T("tool.highlighter"), ToolKind.Highlighter));
        toolsRow.Children.Add(MakeToolButton(GlyphText, Loc.T("tool.text"), ToolKind.Text));
        toolsRow.Children.Add(MakeToolButton(GlyphShape, Loc.T("tool.shape"), ToolKind.Shape));
        toolsRow.Children.Add(MakeToolButton(GlyphLine, Loc.T("tool.line"), ToolKind.Line));
        toolsRow.Children.Add(MakeToolButton(GlyphSticker, Loc.T("tool.sticker"), ToolKind.Sticker));
        toolsRow.Children.Add(MakeToolButton(GlyphBlur, Loc.T("tool.blur"), ToolKind.Blur));
        toolsRow.Children.Add(MakeSeparator());
        _undoButton = MakeActionButton(GlyphUndo, Loc.T("action.undo") + " (" + _settings.UndoHotkey + ")", () => _history.Undo());
        _redoButton = MakeActionButton(GlyphRedo, Loc.T("action.redo") + " (" + _settings.RedoHotkey + ")", () => _history.Redo());
        toolsRow.Children.Add(_undoButton);
        toolsRow.Children.Add(_redoButton);
        toolsRow.Children.Add(MakeSeparator());
        toolsRow.Children.Add(MakeActionButton(GlyphCopy, Loc.T("action.copy") + " (" + _settings.CopyHotkey + ")", DoCopy));
        toolsRow.Children.Add(MakeActionButton(GlyphSave, Loc.T("action.save") + " (" + _settings.SaveHotkey + ")", DoSave));
        toolsRow.Children.Add(MakeActionButton(GlyphPin, Loc.T("action.pin"), DoPin));
        toolsRow.Children.Add(MakeActionButton(GlyphClose, Loc.T("action.close") + " (Esc)", Cancel));
        ToolbarStack.Children.Add(toolsRow);

        // ---- Pen options (width / opacity / colours) ----
        _penOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.width")));
        _widthSlider = new Slider { Minimum = 1, Maximum = 40, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        _widthSlider.ValueChanged += (_, _) => OnWidthChanged();
        _penOptionsPanel.Children.Add(_widthSlider);
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.opacity")));
        _opacitySlider = new Slider { Minimum = 0.1, Maximum = 1.0, Width = 80, VerticalAlignment = VerticalAlignment.Center };
        _opacitySlider.ValueChanged += (_, _) => OnOpacityChanged();
        _penOptionsPanel.Children.Add(_opacitySlider);
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        foreach (var c in Palette) _penOptionsPanel.Children.Add(MakeSwatch(c));
        _penOptionsPanel.Children.Add(MakeMoreColorsButton());
        ToolbarStack.Children.Add(_penOptionsPanel);

        // ---- Blur options (type selector + strength) ----
        _blurOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _blurOptionsPanel.Children.Add(Label(Loc.T("lbl.type")));
        _blurOptionsPanel.Children.Add(MakeBlurKindButton(Loc.T("lbl.gaussian"), BlurKind.Gaussian));
        _blurOptionsPanel.Children.Add(MakeBlurKindButton(Loc.T("lbl.mosaic"), BlurKind.Mosaic));
        _blurOptionsPanel.Children.Add(Label(Loc.T("lbl.blurStrength")));
        _blurSlider = new Slider { Minimum = 2, Maximum = 40, Width = 130, VerticalAlignment = VerticalAlignment.Center, Value = _blurStrength };
        _blurSlider.ValueChanged += (_, e) => _blurStrength = e.NewValue;
        _blurOptionsPanel.Children.Add(_blurSlider);
        ToolbarStack.Children.Add(_blurOptionsPanel);

        // ---- Text options (font / size / style / colour) ----
        _textOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.font")));
        _fontCombo = new ComboBox { Width = 150, VerticalAlignment = VerticalAlignment.Center };
        foreach (var fam in Fonts.SystemFontFamilies
                     .Select(f => f.Source).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            _fontCombo.Items.Add(fam);
        _fontCombo.SelectedItem = _textFont;
        if (_fontCombo.SelectedItem == null) _fontCombo.Text = _textFont;
        _fontCombo.SelectionChanged += (_, _) =>
        {
            if (_fontCombo.SelectedItem is string s) { _textFont = s; ApplyTextStyle(); RefocusText(); }
        };
        _textOptionsPanel.Children.Add(_fontCombo);

        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.size")));
        _textSizeSlider = new Slider { Minimum = 10, Maximum = 96, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _textSize };
        _textSizeSlider.ValueChanged += (_, e) => { _textSize = e.NewValue; ApplyTextStyle(); };
        _textOptionsPanel.Children.Add(_textSizeSlider);

        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.style")));
        _boldButton = MakeStyleToggle("B", () => { _textBold = !_textBold; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _italicButton = MakeStyleToggle("I", () => { _textItalic = !_textItalic; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _strikeButton = MakeStyleToggle("S", () => { _textStrike = !_textStrike; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _textOptionsPanel.Children.Add(_boldButton);
        _textOptionsPanel.Children.Add(_italicButton);
        _textOptionsPanel.Children.Add(_strikeButton);

        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        foreach (var c in Palette) _textOptionsPanel.Children.Add(MakeSwatch(c));
        _textOptionsPanel.Children.Add(MakeMoreColorsButton());
        RefreshStyleToggles();
        ToolbarStack.Children.Add(_textOptionsPanel);

        // ---- Shape options (shape / style / thickness / colour) ----
        _shapeOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _shapeOptionsPanel.Children.Add(Label(Loc.T("lbl.shape")));
        _shapeOptionsPanel.Children.Add(MakeShapeKindButton(Loc.T("shape.rect"), ShapeKind.Rectangle));
        _shapeOptionsPanel.Children.Add(MakeShapeKindButton(Loc.T("shape.rounded"), ShapeKind.RoundedRectangle));
        _shapeOptionsPanel.Children.Add(MakeShapeKindButton(Loc.T("shape.ellipse"), ShapeKind.Ellipse));
        _shapeOptionsPanel.Children.Add(Label(Loc.T("lbl.style")));
        _shapeOptionsPanel.Children.Add(MakeShapeStyleButton(Loc.T("style.outline"), filled: false));
        _shapeOptionsPanel.Children.Add(MakeShapeStyleButton(Loc.T("style.fill"), filled: true));
        _shapeOptionsPanel.Children.Add(Label(Loc.T("lbl.lineWidth")));
        _shapeWidthSlider = new Slider { Minimum = 1, Maximum = 20, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _shapeWidth };
        _shapeWidthSlider.ValueChanged += (_, e) => _shapeWidth = e.NewValue;
        _shapeOptionsPanel.Children.Add(_shapeWidthSlider);
        _shapeOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        foreach (var c in Palette) _shapeOptionsPanel.Children.Add(MakeSwatch(c));
        _shapeOptionsPanel.Children.Add(MakeMoreColorsButton());
        ToolbarStack.Children.Add(_shapeOptionsPanel);

        // ---- Line options (width / arrowheads / colour) ----
        _lineOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _lineOptionsPanel.Children.Add(Label(Loc.T("lbl.lineWidth")));
        _lineWidthSlider = new Slider { Minimum = 1, Maximum = 20, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _lineWidth };
        _lineWidthSlider.ValueChanged += (_, e) => _lineWidth = e.NewValue;
        _lineOptionsPanel.Children.Add(_lineWidthSlider);
        _arrowStartButton = MakeSmallToggle(Loc.T("line.arrowStart"));
        _arrowStartButton.Click += (_, _) => { _lineArrowStart = !_lineArrowStart; RefreshArrowToggles(); };
        _arrowEndButton = MakeSmallToggle(Loc.T("line.arrowEnd"));
        _arrowEndButton.Click += (_, _) => { _lineArrowEnd = !_lineArrowEnd; RefreshArrowToggles(); };
        _lineOptionsPanel.Children.Add(_arrowStartButton);
        _lineOptionsPanel.Children.Add(_arrowEndButton);
        _lineOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        foreach (var c in Palette) _lineOptionsPanel.Children.Add(MakeSwatch(c));
        _lineOptionsPanel.Children.Add(MakeMoreColorsButton());
        RefreshArrowToggles();
        ToolbarStack.Children.Add(_lineOptionsPanel);

        // ---- Sticker options (add image; placed stickers are draggable / wheel-resizable) ----
        _stickerOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var addImage = new Button
        {
            Content = Loc.T("sticker.choose"),
            Padding = new Thickness(10, 3, 10, 3),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 12,
        };
        addImage.Click += (_, _) => AddSticker();
        _stickerOptionsPanel.Children.Add(addImage);
        _stickerOptionsPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("sticker.hint"),
            Foreground = Theme.ForegroundBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        ToolbarStack.Children.Add(_stickerOptionsPanel);

        // The toolbar itself shows a move cursor (drag); interactive controls override it.
        ApplyControlCursors(ToolbarStack);
    }

    /// <summary>Buttons get a hand cursor, sliders/combos an arrow — so only empty
    /// toolbar chrome keeps the drag (SizeAll) cursor.</summary>
    private static void ApplyControlCursors(DependencyObject root)
    {
        foreach (var obj in LogicalTreeHelper.GetChildren(root))
        {
            if (obj is System.Windows.Controls.Primitives.ButtonBase btn)
                btn.Cursor = Cursors.Hand;
            else if (obj is Slider or ComboBox)
                ((FrameworkElement)obj).Cursor = Cursors.Arrow;

            if (obj is DependencyObject child)
                ApplyControlCursors(child);
        }
    }

    private Button MakeShapeKindButton(string text, ShapeKind kind)
    {
        var b = MakeSmallToggle(text);
        b.Tag = kind;
        b.Click += (_, _) => SelectShapeKind(kind);
        _shapeKindButtons.Add(b);
        return b;
    }

    private Button MakeShapeStyleButton(string text, bool filled)
    {
        var b = MakeSmallToggle(text);
        b.Tag = filled;
        b.Click += (_, _) => SelectShapeStyle(filled);
        _shapeStyleButtons.Add(b);
        return b;
    }

    private static Button MakeSmallToggle(string text) => new()
    {
        Content = text,
        Margin = new Thickness(2, 0, 2, 0),
        Padding = new Thickness(8, 2, 8, 2),
        Foreground = Theme.ForegroundBrush,
        Background = Theme.ButtonBgBrush,
        BorderBrush = Theme.ButtonBorderBrush,
        BorderThickness = new Thickness(1),
        FontSize = 12,
    };

    private void SelectShapeKind(ShapeKind kind)
    {
        _shapeKind = kind;
        foreach (var b in _shapeKindButtons)
            b.Background = (ShapeKind)b.Tag! == kind ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private void SelectShapeStyle(bool filled)
    {
        _shapeFilled = filled;
        foreach (var b in _shapeStyleButtons)
            b.Background = (bool)b.Tag! == filled ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private Button MakeStyleToggle(string label, Action onClick)
    {
        var b = new Button
        {
            Content = label,
            Width = 24,
            Height = 24,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(0),
            FontSize = 13,
            FontWeight = label == "B" ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = label == "I" ? FontStyles.Italic : FontStyles.Normal,
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
        };
        if (label == "S") b.Content = new TextBlock { Text = "S", TextDecorations = TextDecorations.Strikethrough };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void RefreshStyleToggles()
    {
        _boldButton.Background = _textBold ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _italicButton.Background = _textItalic ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _strikeButton.Background = _textStrike ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private void RefreshArrowToggles()
    {
        _arrowStartButton.Background = _lineArrowStart ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _arrowEndButton.Background = _lineArrowEnd ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private void PositionToolbar(Point cursor)
    {
        // Force a full layout pass so ActualWidth/Height are valid (the toolbar was just shown).
        Toolbar.UpdateLayout();
        double tw = ToolbarWidth();
        double th = ToolbarHeight();
        const double gap = 14;

        // Place next to the cursor (below-right); flip to the other side if off-screen.
        double x = cursor.X + gap;
        if (x + tw > _vs.Width) x = cursor.X - gap - tw;

        double y = cursor.Y + gap;
        if (y + th > _vs.Height) y = cursor.Y - gap - th;

        ClampToolbar(ref x, ref y);
        Canvas.SetLeft(Toolbar, x);
        Canvas.SetTop(Toolbar, y);
    }

    // Toolbar footprint in canvas (physical px) space: layout size × its counter-scale.
    private double ToolbarWidth() => (Toolbar.ActualWidth > 0 ? Toolbar.ActualWidth : Toolbar.DesiredSize.Width) * _dpiScale;
    private double ToolbarHeight() => (Toolbar.ActualHeight > 0 ? Toolbar.ActualHeight : Toolbar.DesiredSize.Height) * _dpiScale;

    /// <summary>Keep the toolbar fully within the virtual screen, snapped to whole
    /// device pixels — a fractional offset would defeat ClearType pixel alignment.</summary>
    private void ClampToolbar(ref double x, ref double y)
    {
        double tw = ToolbarWidth();
        double th = ToolbarHeight();
        x = Math.Round(Math.Clamp(x, 4, Math.Max(4, _vs.Width - tw - 4)));
        y = Math.Round(Math.Clamp(y, 4, Math.Max(4, _vs.Height - th - 4)));
    }

    // ---- Draggable toolbar ----
    private bool _toolbarDragging;
    private Point _toolbarGrab;

    private void Toolbar_DragStart(object sender, MouseButtonEventArgs e)
    {
        // Only fires for clicks on empty toolbar chrome; buttons/sliders handle their own.
        _toolbarDragging = true;
        var p = e.GetPosition(RootCanvas);
        _toolbarGrab = new Point(p.X - Canvas.GetLeft(Toolbar), p.Y - Canvas.GetTop(Toolbar));
        Toolbar.CaptureMouse();
        e.Handled = true;
    }

    private void Toolbar_DragMove(object sender, MouseEventArgs e)
    {
        if (!_toolbarDragging) return;
        var p = e.GetPosition(RootCanvas);
        double x = p.X - _toolbarGrab.X;
        double y = p.Y - _toolbarGrab.Y;
        ClampToolbar(ref x, ref y);
        Canvas.SetLeft(Toolbar, x);
        Canvas.SetTop(Toolbar, y);
    }

    private void Toolbar_DragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_toolbarDragging) return;
        _toolbarDragging = false;
        Toolbar.ReleaseMouseCapture();
    }

    private Button MakeToolButton(string glyph, string tooltip, ToolKind kind)
    {
        var b = MakeIconButton(glyph, tooltip);
        b.Tag = kind;
        b.Click += (_, _) => SelectTool(kind);
        _toolButtons.Add(b);
        return b;
    }

    private Button MakeActionButton(string glyph, string tooltip, Action action)
    {
        var b = MakeIconButton(glyph, tooltip);
        b.Click += (_, _) => action();
        return b;
    }

    /// <summary>Icon-only button rendered with the Segoe MDL2 Assets glyph font.</summary>
    private static Button MakeIconButton(string glyph, string tooltip) => new()
    {
        Content = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
        Width = 32,
        Height = 28,
        Margin = new Thickness(2, 0, 2, 0),
        Padding = new Thickness(0),
        Foreground = Theme.ForegroundBrush,
        Background = Theme.ButtonBgBrush,
        BorderBrush = Theme.ButtonBorderBrush,
        BorderThickness = new Thickness(1),
        ToolTip = tooltip,
    };

    /// <summary>Small text toggle for choosing the blur sub-type inside the blur tab.</summary>
    private Button MakeBlurKindButton(string text, BlurKind kind)
    {
        var b = new Button
        {
            Content = text,
            Tag = kind,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 12,
        };
        b.Click += (_, _) => SelectBlurKind(kind);
        _blurKindButtons.Add(b);
        return b;
    }

    private static Border MakeSeparator() => new()
    {
        Width = 1,
        Background = Theme.SeparatorBrush,
        Margin = new Thickness(4, 2, 4, 2),
    };

    private static TextBlock Label(string t) => new()
    {
        Text = t,
        Foreground = Theme.ForegroundBrush,
        FontSize = 12,
        Margin = new Thickness(8, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static readonly Color[] Palette =
    {
        Colors.Red, Colors.Orange, Color.FromRgb(0xFF, 0xEB, 0x3B),
        Colors.LimeGreen, Color.FromRgb(0x3D, 0xA9, 0xFC), Colors.Black, Colors.White,
    };

    private Button MakeSwatch(Color c)
    {
        var b = new Button
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(2, 0, 2, 0),
            Background = new SolidColorBrush(c),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        };
        b.Click += (_, _) => OnColorPicked(c);
        return b;
    }

    /// <summary>The "+" swatch that opens the full colour picker (more colours / hex input).</summary>
    private Button MakeMoreColorsButton()
    {
        var b = new Button
        {
            Content = "＋",
            Width = 18,
            Height = 16,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 2, 0),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 11,
            ToolTip = Loc.T("color.more"),
        };
        b.Click += (_, _) => OpenColorPicker();
        return b;
    }

    private void OpenColorPicker()
    {
        Color current;
        double opacity;
        switch (_tool)
        {
            case ToolKind.Highlighter: current = _hlColor; opacity = _hlOpacity; break;
            case ToolKind.Text: current = _textColor; opacity = _textColor.A / 255.0; break;
            case ToolKind.Shape: current = _shapeColor; opacity = _shapeColor.A / 255.0; break;
            case ToolKind.Line: current = _lineColor; opacity = _lineColor.A / 255.0; break;
            default: current = _penColor; opacity = _penOpacity; break;
        }

        var dlg = new ColorPickerWindow(current, opacity, ToolbarScreenRect()) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        ApplyPickedColor(dlg.SelectedColor, dlg.SelectedOpacity);

        // Add the picked colour as a reusable swatch, just before the "+" button.
        var panel = _tool switch
        {
            ToolKind.Text => _textOptionsPanel,
            ToolKind.Shape => _shapeOptionsPanel,
            ToolKind.Line => _lineOptionsPanel,
            _ => _penOptionsPanel,
        };
        panel.Children.Insert(panel.Children.Count - 1, MakeSwatch(dlg.SelectedColor));
    }

    private void ApplyPickedColor(Color rgb, double opacity)
    {
        var a = (byte)Math.Round(opacity * 255);
        if (_tool == ToolKind.Text)
        {
            _textColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            ApplyTextStyle();
            RefocusText();
            return;
        }
        if (_tool == ToolKind.Shape)
        {
            _shapeColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            return;
        }
        if (_tool == ToolKind.Line)
        {
            _lineColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            return;
        }

        if (_tool == ToolKind.Highlighter) { _hlColor = rgb; _hlOpacity = opacity; }
        else { _penColor = rgb; _penOpacity = opacity; }
        _opacitySlider.Value = opacity;   // reflect in the toolbar; triggers ApplyDrawingAttributes
        ApplyDrawingAttributes();
    }

    /// <summary>The toolbar's rectangle in physical screen pixels (for anchoring popups).</summary>
    private Rect ToolbarScreenRect()
    {
        double left = Canvas.GetLeft(Toolbar);
        double top = Canvas.GetTop(Toolbar);
        return new Rect(_vs.X + left, _vs.Y + top, ToolbarWidth(), ToolbarHeight());
    }

    // ---------------------------------------------------------- tool state ---

    private void SelectTool(ToolKind kind)
    {
        if (_editingText != null && kind != ToolKind.Text) CommitActiveText(discardIfEmpty: true);
        _tool = kind;

        foreach (var b in _toolButtons)
        {
            bool active = (ToolKind)b.Tag! == kind;
            b.Background = active ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        }

        bool isPen = kind is ToolKind.MarkerPen or ToolKind.Highlighter;
        bool isBlur = kind is ToolKind.Blur;
        bool isText = kind is ToolKind.Text;
        bool isShape = kind is ToolKind.Shape;
        bool isLine = kind is ToolKind.Line;
        bool isSticker = kind is ToolKind.Sticker;
        // Selection survives tool switches — grabbing works with every tool.

        _penOptionsPanel.Visibility = isPen ? Visibility.Visible : Visibility.Collapsed;
        _blurOptionsPanel.Visibility = isBlur ? Visibility.Visible : Visibility.Collapsed;
        _textOptionsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
        _shapeOptionsPanel.Visibility = isShape ? Visibility.Visible : Visibility.Collapsed;
        _lineOptionsPanel.Visibility = isLine ? Visibility.Visible : Visibility.Collapsed;
        _stickerOptionsPanel.Visibility = isSticker ? Visibility.Visible : Visibility.Collapsed;

        Ink.EditingMode = isPen ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        Ink.IsHitTestVisible = isPen;
        // Region tools capture via InteractionLayer; sticker mode lets clicks reach the
        // sticker images below so they can be dragged/resized. (Annotation grabbing is
        // handled earlier by the tunneling EditLayer handlers, for every tool.)
        InteractionLayer.IsHitTestVisible = isBlur || isText || isShape || isLine;
        InteractionLayer.Cursor = Cursors.Arrow;

        if (isPen) LoadPenControls();
        if (isBlur) SelectBlurKind(_blurKind);
        if (isShape) { SelectShapeKind(_shapeKind); SelectShapeStyle(_shapeFilled); }
        if (isLine) RefreshArrowToggles();
        if (isSticker && StickerHost.Children.Count == 0) AddSticker();
    }

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
        if (_tool == ToolKind.Text) { _textColor = Color.FromArgb(_textColor.A, c.R, c.G, c.B); ApplyTextStyle(); RefocusText(); return; }
        if (_tool == ToolKind.Shape) { _shapeColor = Color.FromArgb(_shapeColor.A, c.R, c.G, c.B); return; }
        if (_tool == ToolKind.Line) { _lineColor = Color.FromArgb(_lineColor.A, c.R, c.G, c.B); return; }
        if (_tool == ToolKind.Highlighter) _hlColor = c;
        else _penColor = c;
        ApplyDrawingAttributes();
    }

    // ------------------------------------------------------ ink undo/redo ---

    private void Ink_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_suppressStrokeHistory) return;
        var stroke = e.Stroke;
        _history.Push(
            undo: () => { _suppressStrokeHistory = true; Ink.Strokes.Remove(stroke); _suppressStrokeHistory = false; },
            redo: () => { _suppressStrokeHistory = true; Ink.Strokes.Add(stroke); _suppressStrokeHistory = false; });
    }

    // ------------------------------------------------------- blur drawing ---

    private void Blur_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool == ToolKind.Text) { PlaceText(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Shape) { Shape_MouseDown(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Line) { Line_MouseDown(e.GetPosition(InteractionLayer)); return; }
        if (_tool != ToolKind.Blur) return;

        _blurDragging = true;
        _blurStart = e.GetPosition(InteractionLayer);
        _blurPreview = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0xA9, 0xFC)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x3D, 0xA9, 0xFC)),
        };
        Canvas.SetLeft(_blurPreview, _blurStart.X);
        Canvas.SetTop(_blurPreview, _blurStart.Y);
        BlurHost.Children.Add(_blurPreview);
        InteractionLayer.CaptureMouse();
    }

    private void Blur_MouseMove(object sender, MouseEventArgs e)
    {
        if (_shapeDragging) { Shape_MouseMove(e.GetPosition(InteractionLayer)); return; }
        if (_lineDragging) { Line_MouseMove(e.GetPosition(InteractionLayer)); return; }
        if (!_blurDragging || _blurPreview == null) return;
        var p = e.GetPosition(InteractionLayer);
        var r = MakeRect(_blurStart, p);
        Canvas.SetLeft(_blurPreview, r.X);
        Canvas.SetTop(_blurPreview, r.Y);
        _blurPreview.Width = r.Width;
        _blurPreview.Height = r.Height;
    }

    private void Blur_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_shapeDragging) { Shape_MouseUp(e.GetPosition(InteractionLayer)); return; }
        if (_lineDragging) { Line_MouseUp(e.GetPosition(InteractionLayer)); return; }
        if (!_blurDragging) return;
        _blurDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        if (_blurPreview != null) BlurHost.Children.Remove(_blurPreview);
        _blurPreview = null;

        var rel = MakeRect(_blurStart, e.GetPosition(InteractionLayer));
        // Clamp to selection.
        rel.Intersect(new Rect(0, 0, _selection.Width, _selection.Height));
        if (rel.Width < 6 || rel.Height < 6) return;

        var abs = new Int32Rect(
            _selection.X + (int)Math.Round(rel.X),
            _selection.Y + (int)Math.Round(rel.Y),
            (int)Math.Round(rel.Width),
            (int)Math.Round(rel.Height));

        FrameworkElement visual = _blurKind == BlurKind.Mosaic
            ? BlurEffects.CreateMosaic(_screenshot, abs, _blurStrength)
            : BlurEffects.CreateGaussian(_screenshot, abs, _blurStrength);

        Canvas.SetLeft(visual, rel.X);
        Canvas.SetTop(visual, rel.Y);
        BlurHost.Children.Add(visual);

        _history.Push(
            undo: () => BlurHost.Children.Remove(visual),
            redo: () => { if (!BlurHost.Children.Contains(visual)) BlurHost.Children.Add(visual); });
    }

    // ------------------------------------------------------- shape tool ---

    private void Shape_MouseDown(Point start)
    {
        _shapeDragging = true;
        _shapeStart = start;

        var brush = new SolidColorBrush(_shapeColor);
        Shape s = _shapeKind == ShapeKind.Ellipse ? new Ellipse() : new Rectangle();
        if (_shapeFilled) { s.Fill = brush; }
        else { s.Fill = Brushes.Transparent; s.Stroke = brush; s.StrokeThickness = _shapeWidth; }

        Canvas.SetLeft(s, start.X);
        Canvas.SetTop(s, start.Y);
        ShapeHost.Children.Add(s);
        _shapePreview = s;
        InteractionLayer.CaptureMouse();
    }

    private void Shape_MouseMove(Point p)
    {
        if (_shapePreview == null) return;
        var r = MakeRect(_shapeStart, p);
        Canvas.SetLeft(_shapePreview, r.X);
        Canvas.SetTop(_shapePreview, r.Y);
        _shapePreview.Width = r.Width;
        _shapePreview.Height = r.Height;
        if (_shapePreview is Rectangle rect && _shapeKind == ShapeKind.RoundedRectangle)
        {
            double radius = Math.Min(16, Math.Min(r.Width, r.Height) / 3.0);
            rect.RadiusX = radius;
            rect.RadiusY = radius;
        }
    }

    private void Shape_MouseUp(Point p)
    {
        _shapeDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        var shape = _shapePreview;
        _shapePreview = null;
        if (shape == null) return;

        var r = MakeRect(_shapeStart, p);
        if (r.Width < 4 || r.Height < 4) { ShapeHost.Children.Remove(shape); return; }

        _history.Push(
            undo: () => ShapeHost.Children.Remove(shape),
            redo: () => { if (!ShapeHost.Children.Contains(shape)) ShapeHost.Children.Add(shape); });
    }

    // -------------------------------------------- direct manipulation ---
    // Hover any annotation (shape / line / text / sticker) with ANY tool: the cursor
    // becomes a move cursor and dragging grabs it; Delete removes the selection. The
    // handlers tunnel (Preview*) on EditLayer, so they run before InkCanvas or the
    // InteractionLayer — drawing only starts on empty space. Hit testing is bounding-
    // box based and moves use a TranslateTransform. Ink strokes and blur regions stay
    // undo-only (moving a blur would silently change which pixels it samples).

    /// <summary>Clicking outside the active text box (and outside the toolbar, so style
    /// tweaks don't dismiss it) commits the text — same as pressing Enter.</summary>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_editingText is not { } box) return;
        if (e.OriginalSource is DependencyObject src &&
            (IsWithin(src, box) || IsWithin(src, Toolbar))) return;
        CommitActiveText(discardIfEmpty: true);
        // Not handled: the same click may then grab an annotation or start a drawing.
    }

    private static bool IsWithin(DependencyObject? node, DependencyObject container)
    {
        while (node != null)
        {
            if (node == container) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private void EditLayer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Editing || _editingText != null) return;
        var p = e.GetPosition(EditLayer);
        var hit = HitAnnotation(p);
        if (hit == null)
        {
            Deselect();   // empty space: clear the selection, then draw as usual
            return;
        }

        _selected = hit;
        UpdateSelectionBox();
        var tt = EnsureTranslate(hit);
        _moveDragging = true;
        _moveGrab = p;
        _moveOrigX = tt.X;
        _moveOrigY = tt.Y;
        EditLayer.CaptureMouse();
        e.Handled = true;
    }

    private void EditLayer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_phase != Phase.Editing) return;
        var p = e.GetPosition(EditLayer);

        if (_moveDragging && _selected != null)
        {
            var tt = EnsureTranslate(_selected);
            tt.X = _moveOrigX + (p.X - _moveGrab.X);
            tt.Y = _moveOrigY + (p.Y - _moveGrab.Y);
            UpdateSelectionBox();
            e.Handled = true;
            return;
        }

        // Hover feedback (idle pointer only — never during a drawing drag).
        if (_editingText == null && e.LeftButton == MouseButtonState.Released)
            EditLayer.Cursor = HitAnnotation(p) != null ? Cursors.SizeAll : null;
    }

    private void EditLayer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_moveDragging) return;
        _moveDragging = false;
        EditLayer.ReleaseMouseCapture();
        e.Handled = true;
        if (_selected is not { } el) return;

        var tt = EnsureTranslate(el);
        double ox = _moveOrigX, oy = _moveOrigY, nx = tt.X, ny = tt.Y;
        if (Math.Abs(nx - ox) < 0.5 && Math.Abs(ny - oy) < 0.5) return;   // click, not a move

        _history.Push(
            undo: () => { var t = EnsureTranslate(el); t.X = ox; t.Y = oy; },
            redo: () => { var t = EnsureTranslate(el); t.X = nx; t.Y = ny; });
    }

    private void DeleteSelected()
    {
        if (_selected is not { } el || el.Parent is not Canvas host) return;
        Deselect();
        host.Children.Remove(el);
        _history.Push(
            undo: () => { if (!host.Children.Contains(el)) host.Children.Add(el); },
            redo: () => host.Children.Remove(el));
    }

    /// <summary>Topmost annotation whose bounds contain <paramref name="p"/> (host z-order:
    /// text above stickers above shapes/lines, matching the composite).</summary>
    private FrameworkElement? HitAnnotation(Point p)
    {
        foreach (var host in new[] { TextHost, StickerHost, ShapeHost })
            for (int i = host.Children.Count - 1; i >= 0; i--)
                if (host.Children[i] is FrameworkElement el && AnnotationBounds(el).Contains(p))
                    return el;
        return null;
    }

    /// <summary>Element bounds in host coordinates, including any move translation.</summary>
    private static Rect AnnotationBounds(FrameworkElement el)
    {
        Rect r;
        if (el is Path { Data: { } g } path)
        {
            r = g.Bounds;   // lines are geometry-positioned, not Canvas-positioned
            r.Inflate(path.StrokeThickness / 2 + 2, path.StrokeThickness / 2 + 2);
        }
        else
        {
            double x = Canvas.GetLeft(el), y = Canvas.GetTop(el);
            r = new Rect(double.IsNaN(x) ? 0 : x, double.IsNaN(y) ? 0 : y,
                el.ActualWidth, el.ActualHeight);
        }
        if (el.RenderTransform is TranslateTransform tt)
        {
            r.X += tt.X;
            r.Y += tt.Y;
        }
        return r;
    }

    private static TranslateTransform EnsureTranslate(FrameworkElement el)
    {
        if (el.RenderTransform is TranslateTransform tt) return tt;
        var t = new TranslateTransform();
        el.RenderTransform = t;
        return t;
    }

    /// <summary>Show/refresh the dashed box around the selection; hides (and clears the
    /// selection) when the element was removed, e.g. by an undo.</summary>
    private void UpdateSelectionBox()
    {
        if (_selected == null || _selected.Parent == null)
        {
            Deselect();
            return;
        }

        if (_selectionBox == null)
        {
            _selectionBox = new Rectangle
            {
                Stroke = new SolidColorBrush(Theme.Accent),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
            };
            EditLayer.Children.Add(_selectionBox);   // above the hosts; hit-test transparent
        }

        var b = AnnotationBounds(_selected);
        b.Inflate(3, 3);
        Canvas.SetLeft(_selectionBox, b.X);
        Canvas.SetTop(_selectionBox, b.Y);
        _selectionBox.Width = Math.Max(0, b.Width);
        _selectionBox.Height = Math.Max(0, b.Height);
        _selectionBox.Visibility = Visibility.Visible;
    }

    private void Deselect()
    {
        _selected = null;
        if (_selectionBox != null) _selectionBox.Visibility = Visibility.Collapsed;
    }

    // -------------------------------------------------------- line tool ---

    private void Line_MouseDown(Point start)
    {
        _lineDragging = true;
        _lineStart = start;
        var brush = new SolidColorBrush(_lineColor);
        _linePreview = new Path
        {
            Stroke = brush,
            Fill = brush,                    // fills the arrowhead triangles
            StrokeThickness = _lineWidth,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        ShapeHost.Children.Add(_linePreview);
        InteractionLayer.CaptureMouse();
    }

    private void Line_MouseMove(Point p)
    {
        if (_linePreview == null) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) p = SnapTo45(_lineStart, p);
        _linePreview.Data = BuildLineGeometry(_lineStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);
    }

    private void Line_MouseUp(Point p)
    {
        _lineDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        var path = _linePreview;
        _linePreview = null;
        if (path == null) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) p = SnapTo45(_lineStart, p);
        if ((p - _lineStart).Length < 8) { ShapeHost.Children.Remove(path); return; }
        path.Data = BuildLineGeometry(_lineStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);

        _history.Push(
            undo: () => ShapeHost.Children.Remove(path),
            redo: () => { if (!ShapeHost.Children.Contains(path)) ShapeHost.Children.Add(path); });
    }

    /// <summary>Line with optional filled arrowheads; the stroked segment is shortened
    /// under each head so the round line cap never pokes past the tip.</summary>
    private static Geometry BuildLineGeometry(Point a, Point b, double width, bool arrowStart, bool arrowEnd)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        Vector d = b - a;
        double len = d.Length;
        if (len < 0.01)
        {
            group.Children.Add(new LineGeometry(a, b));
            return group;
        }
        d /= len;
        var perp = new Vector(-d.Y, d.X);
        // Desired head ≈ max(10, 3.5·width), but never longer than ~half the line —
        // computed with Min(Max(...)) because the two bounds can cross on short drags.
        double head = Math.Min(Math.Max(10, width * 3.5), len * 0.45);
        double halfW = head * 0.45;

        Point lineA = arrowStart ? a + d * (head * 0.8) : a;
        Point lineB = arrowEnd ? b - d * (head * 0.8) : b;
        group.Children.Add(new LineGeometry(lineA, lineB));

        if (arrowStart) group.Children.Add(Arrowhead(a, d, head, halfW, perp));
        if (arrowEnd) group.Children.Add(Arrowhead(b, -d, head, halfW, perp));
        return group;
    }

    /// <summary>Closed triangle with its apex at <paramref name="tip"/>, opening along
    /// <paramref name="inward"/> (unit vector pointing back along the line).</summary>
    private static Geometry Arrowhead(Point tip, Vector inward, double length, double halfWidth, Vector perp)
    {
        Point b1 = tip + inward * length + perp * halfWidth;
        Point b2 = tip + inward * length - perp * halfWidth;
        var figure = new PathFigure(tip,
            new PathSegment[] { new LineSegment(b1, true), new LineSegment(b2, true) },
            closed: true);
        return new PathGeometry(new[] { figure });
    }

    /// <summary>Snap the drag endpoint to the nearest 45° step around the start (Shift).</summary>
    private static Point SnapTo45(Point origin, Point p)
    {
        Vector v = p - origin;
        if (v.Length < 0.01) return p;
        double snapped = Math.Round(Math.Atan2(v.Y, v.X) / (Math.PI / 4)) * (Math.PI / 4);
        return origin + new Vector(Math.Cos(snapped), Math.Sin(snapped)) * v.Length;
    }

    // ----------------------------------------------------- sticker tool ---

    private void AddSticker()
    {
        var dlg = new OpenFileDialog { Title = Loc.T("img.chooseTitle"), Filter = Loc.T("img.filter") };
        if (dlg.ShowDialog() != true) return;

        var src = ImageLoader.TryLoad(dlg.FileName);
        if (src == null)
        {
            MessageBox.Show(this, Loc.T("img.loadFail"), "ScreenPaste",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Fit within ~90% of the selection while keeping aspect ratio.
        double scale = Math.Min(1.0, Math.Min(
            _selection.Width * 0.9 / src.PixelWidth,
            _selection.Height * 0.9 / src.PixelHeight));
        double w = Math.Max(16, src.PixelWidth * scale);
        double h = Math.Max(16, src.PixelHeight * scale);

        var image = new Image { Source = src, Width = w, Height = h, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        Canvas.SetLeft(image, (_selection.Width - w) / 2);
        Canvas.SetTop(image, (_selection.Height - h) / 2);
        AttachStickerInteractions(image);
        StickerHost.Children.Add(image);

        _history.Push(
            undo: () => StickerHost.Children.Remove(image),
            redo: () => { if (!StickerHost.Children.Contains(image)) StickerHost.Children.Add(image); });
    }

    private void AttachStickerInteractions(Image image)
    {
        image.Cursor = Cursors.SizeAll;

        image.MouseLeftButtonDown += (_, e) =>
        {
            if (_tool != ToolKind.Sticker) return;
            _stickerDrag = image;
            var p = e.GetPosition(StickerHost);
            _stickerGrab = new Point(p.X - Canvas.GetLeft(image), p.Y - Canvas.GetTop(image));
            image.CaptureMouse();
            e.Handled = true;
        };
        image.MouseMove += (_, e) =>
        {
            if (_stickerDrag != image) return;
            var p = e.GetPosition(StickerHost);
            Canvas.SetLeft(image, p.X - _stickerGrab.X);
            Canvas.SetTop(image, p.Y - _stickerGrab.Y);
        };
        image.MouseLeftButtonUp += (_, e) =>
        {
            if (_stickerDrag != image) return;
            _stickerDrag = null;
            image.ReleaseMouseCapture();
            e.Handled = true;
        };
        image.MouseWheel += (_, e) =>
        {
            if (_tool != ToolKind.Sticker) return;
            double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            double nw = Math.Max(16, image.Width * factor);
            double nh = Math.Max(16, image.Height * factor);
            // Keep the image centred while scaling.
            Canvas.SetLeft(image, Canvas.GetLeft(image) - (nw - image.Width) / 2);
            Canvas.SetTop(image, Canvas.GetTop(image) - (nh - image.Height) / 2);
            image.Width = nw;
            image.Height = nh;
            e.Handled = true;
        };
    }

    // -------------------------------------------------------- text tool ---

    private void PlaceText(Point rel)
    {
        CommitActiveText(discardIfEmpty: true);

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = 40,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Theme.Accent),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2, 0, 2, 0),
        };
        Canvas.SetLeft(box, rel.X);
        Canvas.SetTop(box, rel.Y);
        TextHost.Children.Add(box);
        _editingText = box;
        ApplyTextStyle();

        // Let the box receive typing (InteractionLayer would otherwise eat clicks).
        InteractionLayer.IsHitTestVisible = false;

        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CommitActiveText(discardIfEmpty: true); e.Handled = true; }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            { CommitActiveText(discardIfEmpty: true); e.Handled = true; }
        };
        // NOTE: no commit-on-focus-loss — clicking a toolbar style button must not
        // dismiss the box. It commits on Enter/Esc, a new placement, tool switch, or export.

        box.Loaded += (_, _) => { box.Focus(); Keyboard.Focus(box); };
        box.Focus();
        Keyboard.Focus(box);
    }

    private void ApplyTextStyle()
    {
        if (_editingText == null) return;
        try { _editingText.FontFamily = new FontFamily(_textFont); } catch { /* keep current */ }
        _editingText.FontSize = _textSize;
        _editingText.Foreground = new SolidColorBrush(_textColor);
        _editingText.FontWeight = _textBold ? FontWeights.Bold : FontWeights.Normal;
        _editingText.FontStyle = _textItalic ? FontStyles.Italic : FontStyles.Normal;
        _editingText.TextDecorations = _textStrike ? TextDecorations.Strikethrough : null;
    }

    /// <summary>Return keyboard focus to the text box being edited so typing continues.</summary>
    private void RefocusText()
    {
        if (_editingText is not { } box) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            box.Focus();
            Keyboard.Focus(box);
            box.CaretIndex = box.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>Finalize the text being edited: bake it into a static label or discard if empty.</summary>
    private void CommitActiveText(bool discardIfEmpty)
    {
        var box = _editingText;
        if (box == null) return;
        _editingText = null;

        if (discardIfEmpty && string.IsNullOrWhiteSpace(box.Text))
        {
            TextHost.Children.Remove(box);
        }
        else
        {
            // Turn the edit box into a non-interactive label that composites cleanly.
            box.IsReadOnly = true;
            box.IsHitTestVisible = false;
            box.Focusable = false;
            box.BorderThickness = new Thickness(0);
            box.Background = Brushes.Transparent;
            box.CaretBrush = Brushes.Transparent;

            _history.Push(
                undo: () => TextHost.Children.Remove(box),
                redo: () => { if (!TextHost.Children.Contains(box)) TextHost.Children.Add(box); });
        }

        // Re-arm click-to-place for the next text (if still on the text tool).
        InteractionLayer.IsHitTestVisible = _tool is ToolKind.Text or ToolKind.Blur;
    }

    // ----------------------------------------------------------- outputs ---

    private BitmapSource Flatten()
    {
        CommitActiveText(discardIfEmpty: true);
        return Compositor.Compose(_screenshot, _selection, Ink.Strokes, BlurHost, ShapeHost, StickerHost, TextHost);
    }

    private void DoCopy()
    {
        var img = Flatten();
        ClipboardService.CopyImage(img);
        PersistSettings();
        Close();
    }

    private void DoSave()
    {
        var img = Flatten();
        var path = FileSaveService.SaveAs(img, _settings.SaveDirectory);
        if (path != null) { PersistSettings(); Close(); }
        else { SelectionBorder.Visibility = Visibility.Visible; }
    }

    private void DoQuickSave()
    {
        var img = Flatten();
        FileSaveService.QuickSave(img, _settings.SaveDirectory);
        PersistSettings();
        Close();
    }

    private void DoPin()
    {
        var img = Flatten();
        int screenX = _vs.X + _selection.X;
        int screenY = _vs.Y + _selection.Y;
        var pin = new PinWindow(img, screenX, screenY, _settings.SaveDirectory);
        pin.Show();
        PersistSettings();
        Close();
    }

    /// <summary>Discard annotations and return to the framing/selection step.</summary>
    private void ResetToSelection()
    {
        CommitActiveText(discardIfEmpty: true);
        Deselect();

        _phase = Phase.Selecting;
        Cursor = Cursors.Cross;

        Ink.Strokes.Clear();
        BlurHost.Children.Clear();
        ShapeHost.Children.Clear();
        StickerHost.Children.Clear();
        TextHost.Children.Clear();
        _history.Clear();

        _selection = default;
        EditLayer.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeReadout.Visibility = Visibility.Collapsed;
        DetectBorder.Visibility = Visibility.Collapsed;
        InteractionLayer.IsHitTestVisible = false;
        Ink.EditingMode = InkCanvasEditingMode.None;

        _dragging = false;
        UpdateMask(null);
    }

    private void Cancel() => Close();

    private void PersistSettings()
    {
        _settings.PenWidth = _penWidth;
        _settings.PenOpacity = _penOpacity;
        _settings.PenColor = ToHex(_penColor);
        _settings.HighlighterWidth = _hlWidth;
        _settings.HighlighterOpacity = _hlOpacity;
        _settings.HighlighterColor = ToHex(_hlColor);
        _settings.GaussianStrength = _blurStrength;
        _settings.TextFont = _textFont;
        _settings.TextSize = _textSize;
        _settings.TextColor = ToHex(_textColor);
        _settings.TextBold = _textBold;
        _settings.TextItalic = _textItalic;
        _settings.TextStrikethrough = _textStrike;
        _settings.ShapeKind = _shapeKind.ToString();
        _settings.ShapeFilled = _shapeFilled;
        _settings.ShapeColor = ToHex(_shapeColor);
        _settings.ShapeWidth = _shapeWidth;
        _settings.LineWidth = _lineWidth;
        _settings.LineColor = ToHex(_lineColor);
        _settings.LineArrowStart = _lineArrowStart;
        _settings.LineArrowEnd = _lineArrowEnd;
        _settings.Save();
    }

    // --------------------------------------------------------- keyboard ---

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Selection cleared first; then editing → back to framing; framing → cancel.
            if (_phase == Phase.Editing && _selected != null) Deselect();
            else if (_phase == Phase.Editing) ResetToSelection();
            else Cancel();
            e.Handled = true;
            return;
        }

        if (_phase != Phase.Editing) return;

        if (e.Key == Key.Delete && _selected != null && _editingText == null)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }

        // Shortcuts are user-configurable in settings.json (parsed into KeyGestures).
        if (Matches(_quickSaveGesture, e)) { DoQuickSave(); e.Handled = true; }
        else if (Matches(_undoGesture, e)) { _history.Undo(); e.Handled = true; }
        else if (Matches(_redoGesture, e)) { _history.Redo(); e.Handled = true; }
        else if (Matches(_copyGesture, e)) { DoCopy(); e.Handled = true; }
        else if (Matches(_saveGesture, e)) { DoSave(); e.Handled = true; }
    }

    private bool Matches(KeyGesture? g, KeyEventArgs e) => g != null && g.Matches(this, e);

    private void UpdateHistoryButtons()
    {
        if (_undoButton != null) _undoButton.IsEnabled = _history.CanUndo;
        if (_redoButton != null) _redoButton.IsEnabled = _history.CanRedo;
        // Undo/redo may have removed, re-added, or repositioned the selected element.
        if (_selected != null) UpdateSelectionBox();
    }

    // ----------------------------------------------------------- helpers ---

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
