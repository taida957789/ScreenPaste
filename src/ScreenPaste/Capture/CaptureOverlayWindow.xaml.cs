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
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

public partial class CaptureOverlayWindow : Window
{
    private enum Phase { Selecting, Editing }

    private BitmapSource _screenshot;   // the backdrop: swapped by F5 and , / . (History.cs)
    private readonly VirtualScreen _vs;
    private readonly AppSettings _settings;

    private double _dpiScale = 1.0;
    private Phase _phase = Phase.Selecting;

    // The chosen region (selection-phase state: CaptureOverlayWindow.Selecting.cs;
    // adjusting it while editing, and right-click: CaptureOverlayWindow.Region.cs)
    private Int32Rect _selection;                 // physical px, screenshot/local coords

    // Editing state
    private ToolKind _tool = ToolKind.MarkerPen;
    private BlurKind _blurKind = BlurKind.Gaussian;
    private readonly EditHistory _history = new();
    private bool _suppressStrokeHistory;

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

    // Magnifier state (framing + region-adjust drags)
    private const int MagSrcW = 30, MagSrcH = 22, MagZoom = 6;   // 30×22 px shown at 180×132
    private byte[]? _pixels;                  // cached BGRA snapshot for pixel lookups
    private int _pixStride;
    private ColorFormatKind _magFormat = ColorFormatKind.Hex;   // a Shift tap cycles HEX → RGB → HSV
    private Color _magColor;

    // Select-tool state (click annotations to move them, Delete to remove)
    private FrameworkElement? _selected;
    private Rectangle? _selectionBox;
    private bool _moveDragging;
    private Point _moveGrab;                  // pointer position at drag start
    private double _moveOrigX, _moveOrigY;    // element translate at drag start

    // Toolbar controls we need to read/update
    private Slider _widthSlider = null!, _opacitySlider = null!, _blurSlider = null!, _textSizeSlider = null!, _shapeWidthSlider = null!, _lineWidthSlider = null!;
    private readonly List<Color> _customColors = new();   // picker-added swatches (persisted)
    private readonly List<Button> _customSwatchButtons = new();   // their buttons, across all panels
    private const int MaxCustomColors = 8;
    private StackPanel _blurOptionsPanel = null!, _penOptionsPanel = null!, _textOptionsPanel = null!, _shapeOptionsPanel = null!, _lineOptionsPanel = null!, _stickerOptionsPanel = null!;
    private Button _arrowStartButton = null!, _arrowEndButton = null!;
    private ComboBox _fontCombo = null!;
    private Button _boldButton = null!, _italicButton = null!, _strikeButton = null!;
    private Button _undoButton = null!, _redoButton = null!;
    private readonly List<Button> _toolButtons = new();
    private readonly List<Button> _blurKindButtons = new();
    private readonly List<Button> _shapeKindButtons = new();
    private readonly List<Button> _shapeStyleButtons = new();

    public CaptureOverlayWindow(CaptureRequest request, AppSettings settings)
    {
        _request = request;
        _screenshot = request.Screenshot;
        _vs = request.Screen;
        _settings = settings;

        InitializeComponent();

        _penWidth = settings.PenWidth;
        _penOpacity = settings.PenOpacity;
        _penColor = ParseColor(settings.PenColor, Colors.Red);
        _hlWidth = settings.HighlighterWidth;
        _hlOpacity = settings.HighlighterOpacity;
        _hlColor = ParseColor(settings.HighlighterColor, Colors.Yellow);
        _blurKind = Enum.TryParse<BlurKind>(settings.BlurKind, out var bk) ? bk : BlurKind.Gaussian;
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
        _magnifyShape = Enum.TryParse<ShapeKind>(settings.MagnifyShape, out var mk) ? mk : ShapeKind.RoundedRectangle;
        _magnifyZoom = Math.Clamp(settings.MagnifyZoom, MagnifyEffects.MinZoom, MagnifyEffects.MaxZoom);
        _magnifyBorderWidth = Math.Clamp(settings.MagnifyBorderWidth, 0, 12);
        _magnifyBorderColor = ParseColor(settings.MagnifyBorderColor, Colors.Red);
        _magnifyConnector = settings.MagnifyConnector;
        _magnifyShadow = settings.MagnifyShadow;
        _magnifySmooth = settings.MagnifySmooth;
        _magnifyWithAnnotations = settings.MagnifyIncludeAnnotations;

        foreach (var hex in settings.CustomColors)
        {
            try { _customColors.Add((Color)ColorConverter.ConvertFromString(hex)); }
            catch { /* skip invalid entries */ }
            if (_customColors.Count >= MaxCustomColors) break;
        }

        InitHistory();   // the backdrop and the pointer

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { Activate(); Focus(); };

        InitSelecting();
        InitRegion();
        InitKeyboard();
        InitOutput();
        InitTools();
        // A resolution / monitor change (common over RDP) invalidates the frozen
        // screenshot and every cached physical coordinate, so the selection would drift
        // against a screen that no longer matches.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) =>
        {
            _elements?.Cancel();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        };

        // Tunneling handlers run before InkCanvas / InteractionLayer see the click:
        // direct-manipulation grab of annotations with ANY tool, and commit-on-click-
        // outside for the text box.
        PreviewMouseLeftButtonDown += Window_PreviewMouseDown;
        // The magnifier keeps following the cursor throughout the editing phase
        // (the selecting phase updates it from RootCanvas_MouseMove).
        PreviewMouseMove += (_, e) =>
        {
            if (_phase == Phase.Editing) UpdateMagnifier(e.GetPosition(RootCanvas));
        };
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
        // Every edit (and every undo/redo) funnels through the history, so this is the one
        // place that has to re-sample the blur and magnifier regions over the new content.
        _history.Changed += InvalidateSampledLayers;

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

        // Same treatment for the magnifier and size chip (UI chrome, not content).
        Magnifier.RenderTransform = new ScaleTransform(_dpiScale, _dpiScale);
        Magnifier.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(Magnifier, TextFormattingMode.Display);
        MagHint1.Text = Loc.T("mag.copy");
        MagHint2.Text = Loc.T("mag.format");
        SizeReadout.RenderTransform = new ScaleTransform(_dpiScale, _dpiScale);
        SizeReadout.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(SizeReadout, TextFormattingMode.Display);

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            _vs.X, _vs.Y, _vs.Width, _vs.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        // Snapshot windows for auto-detect (exclude our own overlay), then walk their
        // UIA trees in the background so hovering can snap to individual UI elements.
        _windows = WindowEnumerator.Enumerate(hwnd);
        _elements = new ElementDetector();
        _elements.StartScan(_windows);
    }

    // ---------------------------------------------------------- edit phase ---

    private void EnterEditing(Rect r)
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

        LayoutEditLayer(sel);
        EditLayer.Visibility = Visibility.Visible;

        EnsureHandles();
        LayoutHandles();
        HandleLayer.Visibility = Visibility.Visible;

        BuildToolbar();
        // Snipaste-style: no tool preselected — dragging inside the region moves it,
        // resizing works via the handles; picking a tool starts annotating.
        SelectTool(ToolKind.None);
        Toolbar.Visibility = Visibility.Visible;   // must be visible before measuring
        PositionToolbar();
        UpdateHistoryButtons();
    }

    private void LayoutEditLayer(Rect sel)
    {
        Canvas.SetLeft(EditLayer, sel.X);
        Canvas.SetTop(EditLayer, sel.Y);
        EditLayer.Width = sel.Width;
        EditLayer.Height = sel.Height;
        Ink.Width = sel.Width;
        Ink.Height = sel.Height;
        InteractionLayer.Width = sel.Width;
        InteractionLayer.Height = sel.Height;
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
    private static readonly string GlyphMagnify = Glyph(0xE71E);     // Zoom (magnifier glass)
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
        _customSwatchButtons.Clear();
        _blurKindButtons.Clear();
        _shapeKindButtons.Clear();
        _shapeStyleButtons.Clear();

        Toolbar.Background = Theme.PanelBrush;
        Toolbar.BorderBrush = Theme.ButtonBorderBrush;

        var toolsRow = new StackPanel { Orientation = Orientation.Horizontal };
        toolsRow.Children.Add(MakeToolButton(GlyphMarker, ToolKind.MarkerPen));
        toolsRow.Children.Add(MakeToolButton(GlyphHighlighter, ToolKind.Highlighter));
        toolsRow.Children.Add(MakeToolButton(GlyphText, ToolKind.Text));
        toolsRow.Children.Add(MakeToolButton(GlyphShape, ToolKind.Shape));
        toolsRow.Children.Add(MakeToolButton(GlyphLine, ToolKind.Line));
        toolsRow.Children.Add(MakeToolButton(GlyphSticker, ToolKind.Sticker));
        toolsRow.Children.Add(MakeToolButton(GlyphBlur, ToolKind.Blur));
        toolsRow.Children.Add(MakeToolButton(GlyphMagnify, ToolKind.Magnify));
        toolsRow.Children.Add(MakeNumberToolButton());
        toolsRow.Children.Add(MakeToolButton(GlyphEraser, ToolKind.Eraser));
        toolsRow.Children.Add(MakeSeparator());
        _undoButton = MakeActionButton(GlyphUndo, Loc.T("action.undo") + " (" + _settings.UndoHotkey + ")", () => _history.Undo());
        _redoButton = MakeActionButton(GlyphRedo, Loc.T("action.redo") + " (" + _settings.RedoHotkey + ")", () => _history.Redo());
        toolsRow.Children.Add(_undoButton);
        toolsRow.Children.Add(_redoButton);
        toolsRow.Children.Add(MakeSeparator());
        AddOutputButtons(toolsRow);
        ToolbarStack.Children.Add(toolsRow);

        // ---- Pen options (width / opacity / colours) ----
        _penOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.width")));
        _widthSlider = new Slider { Minimum = 1, Maximum = 40, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        _widthSlider.ValueChanged += (_, _) => OnWidthChanged();
        _penOptionsPanel.Children.Add(_widthSlider);
        _penOptionsPanel.Children.Add(ValueReadout(_widthSlider, v => v.ToString("0")));
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.opacity")));
        _opacitySlider = new Slider { Minimum = 0.1, Maximum = 1.0, Width = 80, VerticalAlignment = VerticalAlignment.Center };
        _opacitySlider.ValueChanged += (_, _) => OnOpacityChanged();
        _penOptionsPanel.Children.Add(_opacitySlider);
        _penOptionsPanel.Children.Add(ValueReadout(_opacitySlider, v => (v * 100).ToString("0") + "%"));
        _penOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_penOptionsPanel);
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
        _blurOptionsPanel.Children.Add(ValueReadout(_blurSlider, v => v.ToString("0")));
        ToolbarStack.Children.Add(_blurOptionsPanel);

        BuildMagnifyOptions();
        BuildNumberOptions();
        BuildEraserOptions();

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
        _textOptionsPanel.Children.Add(ValueReadout(_textSizeSlider, v => v.ToString("0")));

        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.style")));
        _boldButton = MakeStyleToggle("B", () => { _textBold = !_textBold; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _italicButton = MakeStyleToggle("I", () => { _textItalic = !_textItalic; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _strikeButton = MakeStyleToggle("S", () => { _textStrike = !_textStrike; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _textOptionsPanel.Children.Add(_boldButton);
        _textOptionsPanel.Children.Add(_italicButton);
        _textOptionsPanel.Children.Add(_strikeButton);

        _textOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_textOptionsPanel);
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
        _shapeOptionsPanel.Children.Add(ValueReadout(_shapeWidthSlider, v => v.ToString("0")));
        _shapeOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_shapeOptionsPanel);
        ToolbarStack.Children.Add(_shapeOptionsPanel);

        // ---- Line options (width / arrowheads / colour) ----
        _lineOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _lineOptionsPanel.Children.Add(Label(Loc.T("lbl.lineWidth")));
        _lineWidthSlider = new Slider { Minimum = 1, Maximum = 20, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _lineWidth };
        _lineWidthSlider.ValueChanged += (_, e) => _lineWidth = e.NewValue;
        _lineOptionsPanel.Children.Add(_lineWidthSlider);
        _lineOptionsPanel.Children.Add(ValueReadout(_lineWidthSlider, v => v.ToString("0")));
        _arrowStartButton = MakeSmallToggle(Loc.T("line.arrowStart"));
        _arrowStartButton.Click += (_, _) => { _lineArrowStart = !_lineArrowStart; RefreshArrowToggles(); };
        _arrowEndButton = MakeSmallToggle(Loc.T("line.arrowEnd"));
        _arrowEndButton.Click += (_, _) => { _lineArrowEnd = !_lineArrowEnd; RefreshArrowToggles(); };
        _lineOptionsPanel.Children.Add(_arrowStartButton);
        _lineOptionsPanel.Children.Add(_arrowEndButton);
        _lineOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_lineOptionsPanel);
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

    /// <summary>Snipaste-style placement: the toolbar's right edge aligns with the
    /// selection's right edge, sitting below it; flips above when there is no room,
    /// and tucks inside the selection as a last resort.</summary>
    private void PositionToolbar()
    {
        // Force a full layout pass so ActualWidth/Height are valid (the toolbar was just shown).
        Toolbar.UpdateLayout();
        double tw = ToolbarWidth();
        double th = ToolbarHeight();
        const double gap = 8;

        double x = _selection.X + _selection.Width - tw;
        double y = _selection.Y + _selection.Height + gap;
        if (y + th > _vs.Height) y = _selection.Y - th - gap;                      // above
        if (y < 4) y = Math.Max(4, _selection.Y + _selection.Height - th - gap);   // inside, bottom

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

    private Button MakeToolButton(string glyph, ToolKind kind)
    {
        var b = MakeIconButton(glyph, ToolTipFor(kind));
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

    private static readonly SolidColorBrush SwatchBorder = new(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));

    private Button MakeSwatch(Color c)
    {
        var b = new Button
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(2, 0, 2, 0),
            Background = Theme.SwatchBrush(c),   // checkerboard-backed for translucent colours
            BorderBrush = SwatchBorder,
            BorderThickness = new Thickness(1),
            Tag = c,   // lets RefreshSwatchSelection find and mark the active colour
        };
        b.Click += (_, _) => OnColorPicked(c);
        b.MouseWheel += Swatch_MouseWheel;   // the wheel steps the tool colour's opacity
        return b;
    }

    /// <summary>Standard palette + persisted custom colours + the "+" picker button.</summary>
    private void AddColorSwatches(StackPanel panel)
    {
        foreach (var c in Palette) panel.Children.Add(MakeSwatch(c));
        foreach (var c in _customColors) panel.Children.Add(MakeCustomSwatch(c));
        panel.Children.Add(MakeMoreColorsButton());
    }

    /// <summary>Remember a picker-chosen colour: swatch in every palette, persisted in settings.</summary>
    private void AddCustomColor(Color c)
    {
        if (Palette.Contains(c) || _customColors.Contains(c)) return;
        _customColors.Add(c);
        if (_customColors.Count > MaxCustomColors) _customColors.RemoveAt(0);
        foreach (var panel in SwatchPanels())
            panel.Children.Insert(panel.Children.Count - 1, MakeCustomSwatch(c));   // before the "+"
    }

    /// <summary>A palette swatch that can also be removed with a right-click.</summary>
    private Button MakeCustomSwatch(Color c)
    {
        var b = MakeSwatch(c);
        b.ToolTip = Loc.T("color.removeHint");
        b.MouseRightButtonUp += (_, e) => { RemoveCustomColor(c); e.Handled = true; };
        _customSwatchButtons.Add(b);
        return b;
    }

    private void RemoveCustomColor(Color c)
    {
        _customColors.Remove(c);
        foreach (var b in _customSwatchButtons.Where(b => b.Tag is Color tc && tc == c).ToList())
        {
            if (b.Parent is StackPanel panel) panel.Children.Remove(b);
            _customSwatchButtons.Remove(b);
        }
    }

    /// <summary>The colour the given tool would draw with right now.</summary>
    private Color ToolColor(ToolKind kind) => kind switch
    {
        ToolKind.Highlighter => _hlColor,
        ToolKind.Text => _textColor,
        ToolKind.Shape or ToolKind.Number => _shapeColor,
        ToolKind.Line => _lineColor,
        ToolKind.Magnify => _magnifyBorderColor,
        _ => _penColor,
    };

    /// <summary>Outline the swatch matching the active tool's colour with the accent.</summary>
    private void RefreshSwatchSelection()
    {
        var cur = ToolColor(_tool);
        foreach (var panel in SwatchPanels())
        {
            foreach (var child in panel.Children)
            {
                if (child is not Button b || b.Tag is not Color c) continue;
                bool selected = c.R == cur.R && c.G == cur.G && c.B == cur.B;
                b.BorderBrush = selected ? new SolidColorBrush(Theme.Accent) : SwatchBorder;
                b.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }
    }

    /// <summary>Small numeric readout that tracks a slider's value.</summary>
    private static TextBlock ValueReadout(Slider s, Func<double, string> format)
    {
        var tb = new TextBlock
        {
            Text = format(s.Value),
            Foreground = Theme.ForegroundBrush,
            FontSize = 11,
            MinWidth = 26,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
        };
        s.ValueChanged += (_, e) => tb.Text = format(e.NewValue);
        return tb;
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
        b.MouseWheel += Swatch_MouseWheel;
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
            case ToolKind.Shape: case ToolKind.Number: current = _shapeColor; opacity = _shapeColor.A / 255.0; break;
            case ToolKind.Line: current = _lineColor; opacity = _lineColor.A / 255.0; break;
            case ToolKind.Magnify: current = _magnifyBorderColor; opacity = _magnifyBorderColor.A / 255.0; break;
            default: current = _penColor; opacity = _penOpacity; break;
        }

        var dlg = new ColorPickerWindow(current, opacity, ToolbarScreenRect()) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        ApplyPickedColor(dlg.SelectedColor, dlg.SelectedOpacity);

        // Reusable swatch in every palette, remembered across sessions.
        AddCustomColor(dlg.SelectedColor);
        RefreshSwatchSelection();
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
        if (_tool is ToolKind.Shape or ToolKind.Number)
        {
            _shapeColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            return;
        }
        if (_tool == ToolKind.Line)
        {
            _lineColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            return;
        }
        if (_tool == ToolKind.Magnify)
        {
            _magnifyBorderColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
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

    // ------------------------------------------------------ ink undo/redo ---

    private void Ink_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (!_suppressStrokeHistory) PushStrokeHistory(e.Stroke);
    }

    /// <summary>Record a stroke already on the canvas (drawn, or a Shift straight segment).</summary>
    private void PushStrokeHistory(Stroke stroke)
    {
        _history.Push(
            undo: () => { _suppressStrokeHistory = true; Ink.Strokes.Remove(stroke); _suppressStrokeHistory = false; },
            redo: () => { _suppressStrokeHistory = true; Ink.Strokes.Add(stroke); _suppressStrokeHistory = false; });
    }

    // ------------------------------------------------------- blur drawing ---

    private void Blur_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool == ToolKind.None) { Interior_MouseDown(e); return; }
        if (_tool == ToolKind.Text) { PlaceText(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Shape) { Shape_MouseDown(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Line) { Line_MouseDown(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Magnify) { Magnify_MouseDown(e.GetPosition(InteractionLayer)); return; }
        if (_tool == ToolKind.Number) { PlaceNumber(e.GetPosition(InteractionLayer)); return; }
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
        if (_regionMoving) { Region_MoveTo(e.GetPosition(RootCanvas)); return; }
        if (_shapeDragging) { Shape_MouseMove(e.GetPosition(InteractionLayer)); return; }
        if (_lineDragging) { Line_MouseMove(e.GetPosition(InteractionLayer)); return; }
        if (_magnifyDragging) { Magnify_MouseMove(e.GetPosition(InteractionLayer)); return; }
        if (!_blurDragging || _blurPreview == null) return;
        var r = DragRect(_blurStart, e.GetPosition(InteractionLayer));
        Canvas.SetLeft(_blurPreview, r.X);
        Canvas.SetTop(_blurPreview, r.Y);
        _blurPreview.Width = r.Width;
        _blurPreview.Height = r.Height;
    }

    private void Blur_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_regionMoving) { Region_MoveEnd(); return; }
        if (_shapeDragging) { Shape_MouseUp(e.GetPosition(InteractionLayer)); return; }
        if (_lineDragging) { Line_MouseUp(e.GetPosition(InteractionLayer)); return; }
        if (_magnifyDragging) { Magnify_MouseUp(e.GetPosition(InteractionLayer)); return; }
        if (!_blurDragging) return;
        _blurDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        if (_blurPreview != null) BlurHost.Children.Remove(_blurPreview);
        _blurPreview = null;

        var rel = DragRect(_blurStart, e.GetPosition(InteractionLayer));
        // Clamp to selection.
        rel.Intersect(new Rect(0, 0, _selection.Width, _selection.Height));
        if (rel.Width < 6 || rel.Height < 6) return;
        RecordAdded(BlurHost, CreateBlur(rel));   // the history push re-samples it
    }

    /// <summary>A blur region in the blur tool's kind and strength over <paramref name="rel"/>,
    /// added to its layer; the caller records it. Region-local coords: the region samples the
    /// composite beneath it, not the raw screenshot, so it stays valid wherever it is dragged.</summary>
    private FrameworkElement CreateBlur(Rect rel)
    {
        var visual = BlurEffects.Create(_blurKind, rel, _blurStrength);
        BlurHost.Children.Add(visual);
        return visual;
    }

    // -------------------------------------------------- layer sampling ---
    // Two layers re-read what currently sits under them: a blur region shows the pixels it
    // covers (so it hides the annotations beneath it and can be moved freely), and a
    // magnifier shows the pixels it was pointed at, enlarged. `_blurBeneath` /
    // `_magnifyBeneath` cache those composites and are dropped whenever an edit lands; a
    // drag only re-crops from the cache, which keeps it cheap per frame.

    private BitmapSource? _blurBeneath;

    private void InvalidateSampledLayers()
    {
        _blurBeneath = null;
        _magnifyBeneath = null;
        _regionCrop = null;
        RefreshBlurs();
        // Re-sampling assigns new bitmaps to the blur images, which invalidates their layout.
        // RenderTargetBitmap draws whatever was last *arranged*, so the pending pass has to be
        // flushed before the magnifiers rasterise BlurHost — otherwise a magnifier caches a
        // stale (or zero-sized) copy of a blur that was just created, until the next edit.
        if (MagnifyHost.Children.Count > 0) EditLayer.UpdateLayout();
        RefreshMagnifiers();   // after the blurs: a magnifier can enlarge blurred pixels
    }

    private BitmapSource BlurBeneath() => _blurBeneath ??= Compositor.ComposeBeneathBlur(
        _screenshot, _selection, ShownCursor(), Ink.Strokes, ShapeHost, StickerHost, TextHost);

    /// <summary>Re-sample every blur region from the content beneath the blur layer.</summary>
    private void RefreshBlurs()
    {
        if (_phase != Phase.Editing || BlurHost.Children.Count == 0) return;

        var beneath = BlurBeneath();
        foreach (var child in BlurHost.Children)
            if (child is FrameworkElement el && el != _blurPreview)
                BlurEffects.Resample(el, beneath);
    }

    // ------------------------------------------------------- shape tool ---

    private void Shape_MouseDown(Point start)
    {
        _shapeDragging = true;
        _shapeStart = start;
        _shapePreview = CreateShape(new Rect(start, start));
        InteractionLayer.CaptureMouse();
    }

    /// <summary>A rectangle or ellipse in the shape tool's style over <paramref name="r"/>,
    /// added to the shape layer; the caller records it.</summary>
    private Shape CreateShape(Rect r)
    {
        var brush = new SolidColorBrush(_shapeColor);
        Shape s = _shapeKind == ShapeKind.Ellipse ? new Ellipse() : new Rectangle();
        if (_shapeFilled) { s.Fill = brush; }
        else { s.Fill = Brushes.Transparent; s.Stroke = brush; s.StrokeThickness = _shapeWidth; }
        LayoutShape(s, r, _shapeKind == ShapeKind.RoundedRectangle);
        ShapeHost.Children.Add(s);
        return s;
    }

    private void Shape_MouseMove(Point p)
    {
        if (_shapePreview == null) return;
        LayoutShape(_shapePreview, DragRect(_shapeStart, p), _shapeKind == ShapeKind.RoundedRectangle);
    }

    /// <summary>Place a rectangle or ellipse; a rounded rectangle's corners follow its size.</summary>
    private static void LayoutShape(Shape shape, Rect r, bool rounded)
    {
        Canvas.SetLeft(shape, r.X);
        Canvas.SetTop(shape, r.Y);
        shape.Width = r.Width;
        shape.Height = r.Height;
        if (shape is Rectangle rect && rounded)
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

        var r = DragRect(_shapeStart, p);
        if (r.Width < MinShapeSize || r.Height < MinShapeSize) { ShapeHost.Children.Remove(shape); return; }
        RecordAdded(ShapeHost, shape);
    }

    // -------------------------------------------------------- magnifier ---
    // Follows the cursor while framing and while dragging the region handles: a 6×
    // pixel view with a translucent crosshair and a white box on the hovered pixel,
    // plus a readout panel (position, colour, copy/format hints). C copies the colour,
    // a Shift tap cycles its notation (keys: CaptureOverlayWindow.Keyboard.cs).

    private void EnsurePixelCache()
    {
        if (_pixels != null) return;
        _pixStride = _screenshot.PixelWidth * 4;
        _pixels = new byte[_pixStride * _screenshot.PixelHeight];
        _screenshot.CopyPixels(_pixels, _pixStride, 0);
    }

    private Color PixelAt(int x, int y)
    {
        int i = y * _pixStride + x * 4;   // Pbgra32, opaque screen pixels
        return Color.FromRgb(_pixels![i + 2], _pixels[i + 1], _pixels[i]);
    }

    private void UpdateMagnifier(Point p)
    {
        int px = Math.Clamp((int)Math.Round(p.X), 0, _screenshot.PixelWidth - 1);
        int py = Math.Clamp((int)Math.Round(p.Y), 0, _screenshot.PixelHeight - 1);
        EnsurePixelCache();

        // Magnified view: crop stays inside the screenshot; the marker shifts instead,
        // so the hovered pixel is always the one outlined even at screen edges.
        int sx = Math.Clamp(px - MagSrcW / 2, 0, _screenshot.PixelWidth - MagSrcW);
        int sy = Math.Clamp(py - MagSrcH / 2, 0, _screenshot.PixelHeight - MagSrcH);
        MagImage.Source = new CroppedBitmap(_screenshot, new Int32Rect(sx, sy, MagSrcW, MagSrcH));

        double mx = (px - sx) * MagZoom;
        double my = (py - sy) * MagZoom;
        const double viewW = MagSrcW * MagZoom, viewH = MagSrcH * MagZoom;
        // Four segments stopping at the centre cell — no blue tint on the pixel itself.
        Canvas.SetLeft(MagCrossT, mx);
        Canvas.SetTop(MagCrossT, 0);
        MagCrossT.Height = Math.Max(0, my);
        Canvas.SetLeft(MagCrossB, mx);
        Canvas.SetTop(MagCrossB, my + MagZoom);
        MagCrossB.Height = Math.Max(0, viewH - my - MagZoom);
        Canvas.SetLeft(MagCrossL, 0);
        Canvas.SetTop(MagCrossL, my);
        MagCrossL.Width = Math.Max(0, mx);
        Canvas.SetLeft(MagCrossR, mx + MagZoom);
        Canvas.SetTop(MagCrossR, my);
        MagCrossR.Width = Math.Max(0, viewW - mx - MagZoom);
        Canvas.SetLeft(MagCenter, mx);
        Canvas.SetTop(MagCenter, my);

        _magColor = PixelAt(px, py);
        MagSwatch.Fill = new SolidColorBrush(_magColor);
        MagPos.Text = $"({_vs.X + px}, {_vs.Y + py})";
        RefreshMagColorText();

        UpdateGuideLines(px, py);
        PositionMagnifier(px, py);
        Magnifier.Visibility = Visibility.Visible;
    }

    private void RefreshMagColorText() => MagColor.Text = ColorFormat.Format(_magColor, _magFormat);

    /// <summary>Bottom-right of the cursor by default; flips left/up near screen edges.</summary>
    private void PositionMagnifier(int px, int py)
    {
        if (Magnifier.ActualWidth <= 0) Magnifier.UpdateLayout();
        double w = Magnifier.ActualWidth * _dpiScale;    // physical footprint (counter-scaled UI)
        double h = Magnifier.ActualHeight * _dpiScale;
        double gap = 20 * _dpiScale;

        double x = px + gap;
        if (x + w > _vs.Width) x = px - gap - w;
        double y = py + gap;
        if (y + h > _vs.Height) y = py - gap - h;

        Canvas.SetLeft(Magnifier, Math.Round(Math.Max(2, x)));
        Canvas.SetTop(Magnifier, Math.Round(Math.Max(2, y)));
    }

    private void CopyMagColor()
    {
        try
        {
            Clipboard.SetText(ColorFormat.Format(_magColor, _magFormat));
        }
        catch (Exception ex)
        {
            // Diagnosable rather than silent: clipboard failures land in crash.log.
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppSettings.ConfigDirectory, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] magnifier copy failed: {ex}{Environment.NewLine}");
            }
            catch { /* never fail while reporting a failure */ }
        }
    }

    // -------------------------------------------- direct manipulation ---
    // Hover any annotation (blur / shape / line / text / sticker) with ANY tool: the
    // cursor becomes a move cursor and dragging grabs it; Delete removes the selection.
    // The handlers tunnel (Preview*) on EditLayer, so they run before InkCanvas or the
    // InteractionLayer — drawing only starts on empty space. Hit testing is bounding-
    // box based and moves use a TranslateTransform. A dragged blur re-samples from its
    // new position each frame. Ink strokes stay undo-only.

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
        var hit = HitAnnotation(p, ToolGrabs);

        // A magnifier's framed source is grabbable too — dragging it re-points the enlarged
        // view at different content. It is drawn in the top layer, so it wins over anything
        // below the magnifier host, while the enlarged views themselves stay above it.
        if ((hit == null || hit.Parent != MagnifyHost) && HitMagnifySource(p) is { } mag && ToolGrabs(mag))
        {
            _selected = mag;
            UpdateSelectionBox();
            _magnifySourceDrag = mag;
            _magnifySourceGrab = p;
            _magnifySourceStart = MagnifyEffects.SpecOf(mag)!.Source;
            EditLayer.CaptureMouse();
            e.Handled = true;
            return;
        }

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

        if (_magnifySourceDrag is { } mag)
        {
            var (sdx, sdy) = MoveDelta(_magnifySourceGrab, p);
            MagnifyEffects.MoveSourceTo(mag, ClampSourceToRegion(new Rect(
                _magnifySourceStart.X + sdx, _magnifySourceStart.Y + sdy,
                _magnifySourceStart.Width, _magnifySourceStart.Height)).TopLeft);
            ResampleMagnifier(mag);   // it now frames different pixels
            LayoutMagnifySourceHandles();
            e.Handled = true;
            return;
        }

        if (_moveDragging && _selected != null)
        {
            var (dx, dy) = MoveDelta(_moveGrab, p);
            var tt = EnsureTranslate(_selected);
            tt.X = _moveOrigX + dx;
            tt.Y = _moveOrigY + dy;
            AfterAnnotationMoved(_selected);
            e.Handled = true;
            return;
        }

        // Hover feedback (idle pointer only — never during a drawing drag).
        if (_editingText == null && e.LeftButton == MouseButtonState.Released)
        {
            bool grabbable = HitAnnotation(p, ToolGrabs) != null || HitMagnifySource(p) is { } source && ToolGrabs(source);
            EditLayer.Cursor = grabbable ? Cursors.SizeAll : null;
            // Over a magnifier's framed source the element under the pointer is usually
            // InteractionLayer, and an explicit Cursor on the hit element beats the one
            // inherited from EditLayer -- so the hint has to be set there too or the frame
            // looks immovable.
            InteractionLayer.Cursor = grabbable ? Cursors.SizeAll : DefaultInteractionCursor();
        }
    }

    private void EditLayer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_magnifySourceDrag is { } mag)
        {
            _magnifySourceDrag = null;
            EditLayer.ReleaseMouseCapture();
            e.Handled = true;

            var before = _magnifySourceStart;
            var now = MagnifyEffects.SpecOf(mag)!.Source;
            if (Math.Abs(now.X - before.X) < 0.5 && Math.Abs(now.Y - before.Y) < 0.5) return;

            // The history's Changed hook re-samples, so the commands only move the frame.
            _history.Push(
                undo: () => MagnifyEffects.MoveSourceTo(mag, before.TopLeft),
                redo: () => MagnifyEffects.MoveSourceTo(mag, now.TopLeft));
            return;
        }

        if (!_moveDragging) return;
        _moveDragging = false;
        EditLayer.ReleaseMouseCapture();
        e.Handled = true;
        if (_selected is not { } el) return;

        PushMove(el, new Point(_moveOrigX, _moveOrigY));
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
    /// magnifier above blur above text above stickers above shapes/lines, matching the
    /// composite), skipping any <paramref name="accept"/> turns down. A magnifier is only
    /// grabbed by its enlarged view, not by its frame.</summary>
    private FrameworkElement? HitAnnotation(Point p, Func<FrameworkElement, bool>? accept = null)
    {
        foreach (var host in new[] { MagnifyHost, BlurHost, TextHost, StickerHost, ShapeHost })
            for (int i = host.Children.Count - 1; i >= 0; i--)
                if (host.Children[i] is FrameworkElement el && AnnotationBounds(el).Contains(p) &&
                    (accept == null || accept(el)))
                    return el;
        return null;
    }

    /// <summary>Whether a press with the current tool picks <paramref name="el"/> up: the
    /// number tool only picks up number markers and numbers what lies under the others.</summary>
    private bool ToolGrabs(FrameworkElement el) =>
        NumberMarker.PressGrabs(_tool, NumberMarker.SpecOf(el) != null);

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

        LayoutMagnifySourceHandles();

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
        MagnifySourceHandleLayer.Visibility = Visibility.Collapsed;
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
        RecordAdded(StickerHost, image);
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
        // The overlay turns the IME off so shortcut letters arrive as keys; typing a
        // Chinese/Japanese annotation needs it back on here.
        InputMethod.SetIsInputMethodEnabled(box, true);

        // Esc and Enter (not Shift+Enter) commit through the overlay's key router.
        // NOTE: no commit-on-focus-loss — clicking a toolbar style button must not
        // dismiss the box. It commits on Enter/Esc, a new placement, tool switch, or export.

        box.PreviewMouseDown += TextBox_PreviewMiddleDown;   // middle-click pastes the clipboard text
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
        InteractionLayer.IsHitTestVisible = _tool is ToolKind.Text or ToolKind.Blur or ToolKind.Magnify;
    }

    // ---------------------------------------------------------- history ---

    private void UpdateHistoryButtons()
    {
        if (_undoButton != null) _undoButton.IsEnabled = _history.CanUndo;
        if (_redoButton != null) _redoButton.IsEnabled = _history.CanRedo;
        // Undo/redo may have removed, re-added, or repositioned the selected element.
        if (_selected != null) UpdateSelectionBox();
    }

    // ----------------------------------------------------------- helpers ---

    private static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
