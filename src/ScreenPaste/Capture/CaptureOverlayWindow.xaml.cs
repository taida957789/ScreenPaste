using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

    // Toolbar controls we need to read/update
    private Slider _widthSlider = null!, _opacitySlider = null!, _blurSlider = null!, _textSizeSlider = null!;
    private StackPanel _blurOptionsPanel = null!, _penOptionsPanel = null!, _textOptionsPanel = null!;
    private ComboBox _fontCombo = null!;
    private Button _boldButton = null!, _italicButton = null!, _strikeButton = null!;
    private Button _redoButton = null!;
    private readonly List<Button> _toolButtons = new();
    private readonly List<Button> _blurKindButtons = new();

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

        Ink.StrokeCollected += Ink_StrokeCollected;
        InteractionLayer.MouseLeftButtonDown += Blur_MouseDown;
        InteractionLayer.MouseMove += Blur_MouseMove;
        InteractionLayer.MouseLeftButtonUp += Blur_MouseUp;

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
        PositionToolbar(cursor);
        Toolbar.Visibility = Visibility.Visible;
        UpdateHistoryButtons();
    }

    // -------------------------------------------------------------- toolbar ---

    // Segoe MDL2 Assets glyphs (built into Win10/11) — icon-only toolbar.
    private static string Glyph(int code) => char.ConvertFromUtf32(code);
    private static readonly string GlyphMarker = Glyph(0xE70F);      // Edit (pen)
    private static readonly string GlyphHighlighter = Glyph(0xE891); // Highlight
    private static readonly string GlyphText = Glyph(0xE8D2);        // Font (A)
    private static readonly string GlyphBlur = Glyph(0xE7B3);        // blur metaphor
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

        Toolbar.Background = Theme.PanelBrush;
        Toolbar.BorderBrush = Theme.ButtonBorderBrush;

        var toolsRow = new StackPanel { Orientation = Orientation.Horizontal };
        toolsRow.Children.Add(MakeToolButton(GlyphMarker, "麥克筆", ToolKind.MarkerPen));
        toolsRow.Children.Add(MakeToolButton(GlyphHighlighter, "螢光筆", ToolKind.Highlighter));
        toolsRow.Children.Add(MakeToolButton(GlyphText, "文字", ToolKind.Text));
        toolsRow.Children.Add(MakeToolButton(GlyphBlur, "模糊", ToolKind.Blur));
        toolsRow.Children.Add(MakeSeparator());
        // 復原改由設定中的熱鍵觸發（預設 Ctrl+Z），不再放工具列按鈕。
        _redoButton = MakeActionButton(GlyphRedo, "重做 (" + _settings.RedoHotkey + ")", () => _history.Redo());
        toolsRow.Children.Add(_redoButton);
        toolsRow.Children.Add(MakeSeparator());
        toolsRow.Children.Add(MakeActionButton(GlyphCopy, "複製 (" + _settings.CopyHotkey + ")", DoCopy));
        toolsRow.Children.Add(MakeActionButton(GlyphSave, "儲存 (" + _settings.SaveHotkey + ")", DoSave));
        toolsRow.Children.Add(MakeActionButton(GlyphPin, "釘選到螢幕", DoPin));
        toolsRow.Children.Add(MakeActionButton(GlyphClose, "關閉 (Esc)", Cancel));
        ToolbarStack.Children.Add(toolsRow);

        // ---- Pen options (width / opacity / colours) ----
        _penOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _penOptionsPanel.Children.Add(Label("粗細"));
        _widthSlider = new Slider { Minimum = 1, Maximum = 40, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        _widthSlider.ValueChanged += (_, _) => OnWidthChanged();
        _penOptionsPanel.Children.Add(_widthSlider);
        _penOptionsPanel.Children.Add(Label("透明度"));
        _opacitySlider = new Slider { Minimum = 0.1, Maximum = 1.0, Width = 80, VerticalAlignment = VerticalAlignment.Center };
        _opacitySlider.ValueChanged += (_, _) => OnOpacityChanged();
        _penOptionsPanel.Children.Add(_opacitySlider);
        _penOptionsPanel.Children.Add(Label("顏色"));
        foreach (var c in Palette) _penOptionsPanel.Children.Add(MakeSwatch(c));
        _penOptionsPanel.Children.Add(MakeMoreColorsButton());
        ToolbarStack.Children.Add(_penOptionsPanel);

        // ---- Blur options (type selector + strength) ----
        _blurOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _blurOptionsPanel.Children.Add(Label("類型"));
        _blurOptionsPanel.Children.Add(MakeBlurKindButton("高斯", BlurKind.Gaussian));
        _blurOptionsPanel.Children.Add(MakeBlurKindButton("馬賽克", BlurKind.Mosaic));
        _blurOptionsPanel.Children.Add(Label("模糊程度"));
        _blurSlider = new Slider { Minimum = 2, Maximum = 40, Width = 130, VerticalAlignment = VerticalAlignment.Center, Value = _blurStrength };
        _blurSlider.ValueChanged += (_, e) => _blurStrength = e.NewValue;
        _blurOptionsPanel.Children.Add(_blurSlider);
        ToolbarStack.Children.Add(_blurOptionsPanel);

        // ---- Text options (font / size / style / colour) ----
        _textOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _textOptionsPanel.Children.Add(Label("字體"));
        _fontCombo = new ComboBox { Width = 150, VerticalAlignment = VerticalAlignment.Center };
        foreach (var fam in Fonts.SystemFontFamilies
                     .Select(f => f.Source).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            _fontCombo.Items.Add(fam);
        _fontCombo.SelectedItem = _textFont;
        if (_fontCombo.SelectedItem == null) _fontCombo.Text = _textFont;
        _fontCombo.SelectionChanged += (_, _) =>
        {
            if (_fontCombo.SelectedItem is string s) { _textFont = s; ApplyTextStyle(); }
        };
        _textOptionsPanel.Children.Add(_fontCombo);

        _textOptionsPanel.Children.Add(Label("大小"));
        _textSizeSlider = new Slider { Minimum = 10, Maximum = 96, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _textSize };
        _textSizeSlider.ValueChanged += (_, e) => { _textSize = e.NewValue; ApplyTextStyle(); };
        _textOptionsPanel.Children.Add(_textSizeSlider);

        _textOptionsPanel.Children.Add(Label("樣式"));
        _boldButton = MakeStyleToggle("B", () => { _textBold = !_textBold; RefreshStyleToggles(); ApplyTextStyle(); });
        _italicButton = MakeStyleToggle("I", () => { _textItalic = !_textItalic; RefreshStyleToggles(); ApplyTextStyle(); });
        _strikeButton = MakeStyleToggle("S", () => { _textStrike = !_textStrike; RefreshStyleToggles(); ApplyTextStyle(); });
        _textOptionsPanel.Children.Add(_boldButton);
        _textOptionsPanel.Children.Add(_italicButton);
        _textOptionsPanel.Children.Add(_strikeButton);

        _textOptionsPanel.Children.Add(Label("顏色"));
        foreach (var c in Palette) _textOptionsPanel.Children.Add(MakeSwatch(c));
        _textOptionsPanel.Children.Add(MakeMoreColorsButton());
        RefreshStyleToggles();
        ToolbarStack.Children.Add(_textOptionsPanel);
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

    private void PositionToolbar(Point cursor)
    {
        Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tw = Toolbar.DesiredSize.Width;
        double th = Toolbar.DesiredSize.Height;
        const double gap = 14;

        // Place next to the cursor (below-right); flip to the other side if off-screen.
        double x = cursor.X + gap;
        if (x + tw > _vs.Width) x = cursor.X - gap - tw;

        double y = cursor.Y + gap;
        if (y + th > _vs.Height) y = cursor.Y - gap - th;

        x = Math.Clamp(x, 4, Math.Max(4, _vs.Width - tw - 4));
        y = Math.Clamp(y, 4, Math.Max(4, _vs.Height - th - 4));

        Canvas.SetLeft(Toolbar, x);
        Canvas.SetTop(Toolbar, y);
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
            ToolTip = "更多顏色 / 輸入 Hex",
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
            default: current = _penColor; opacity = _penOpacity; break;
        }

        var dlg = new ColorPickerWindow(current, opacity, ToolbarScreenRect()) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        ApplyPickedColor(dlg.SelectedColor, dlg.SelectedOpacity);

        // Add the picked colour as a reusable swatch, just before the "+" button.
        var panel = _tool == ToolKind.Text ? _textOptionsPanel : _penOptionsPanel;
        panel.Children.Insert(panel.Children.Count - 1, MakeSwatch(dlg.SelectedColor));
    }

    private void ApplyPickedColor(Color rgb, double opacity)
    {
        if (_tool == ToolKind.Text)
        {
            var a = (byte)Math.Round(opacity * 255);
            _textColor = Color.FromArgb(a, rgb.R, rgb.G, rgb.B);
            ApplyTextStyle();
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
        return new Rect(_vs.X + left, _vs.Y + top, Toolbar.ActualWidth, Toolbar.ActualHeight);
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

        _penOptionsPanel.Visibility = isPen ? Visibility.Visible : Visibility.Collapsed;
        _blurOptionsPanel.Visibility = isBlur ? Visibility.Visible : Visibility.Collapsed;
        _textOptionsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;

        Ink.EditingMode = isPen ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        Ink.IsHitTestVisible = isPen;
        InteractionLayer.IsHitTestVisible = isBlur || isText;

        if (isPen) LoadPenControls();
        if (isBlur) SelectBlurKind(_blurKind);
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
        if (_tool == ToolKind.Text) { _textColor = c; ApplyTextStyle(); return; }
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
        box.LostKeyboardFocus += (_, _) => CommitActiveText(discardIfEmpty: true);

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
        return Compositor.Compose(_screenshot, _selection, Ink.Strokes, BlurHost, TextHost);
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

        _phase = Phase.Selecting;
        Cursor = Cursors.Cross;

        Ink.Strokes.Clear();
        BlurHost.Children.Clear();
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
        _settings.Save();
    }

    // --------------------------------------------------------- keyboard ---

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Editing → back to the selection step; selecting → cancel the whole capture.
            if (_phase == Phase.Editing) ResetToSelection();
            else Cancel();
            e.Handled = true;
            return;
        }

        if (_phase != Phase.Editing) return;

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
        if (_redoButton != null) _redoButton.IsEnabled = _history.CanRedo;
    }

    // ----------------------------------------------------------- helpers ---

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
