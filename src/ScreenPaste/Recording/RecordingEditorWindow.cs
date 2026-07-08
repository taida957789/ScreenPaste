using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using ScreenPaste.Settings;
using Forms = System.Windows.Forms;
using Path = System.IO.Path;
using ShapesPath = System.Windows.Shapes.Path;

namespace ScreenPaste.Recording;

/// <summary>
/// Post-recording editor: previews the intermediate clip (MediaElement), trims via
/// draggable timeline handles, and exports the selected span to GIF/MP4/WebP through
/// ffmpeg with determinate progress. The intermediate temp file is deleted on close.
/// </summary>
public sealed class RecordingEditorWindow : Window
{
    private const double HandleHitPx = 8;     // grab tolerance around a trim handle
    private const double HandleW = 10, HandleH = 22;
    private const double TimelineH = 36, TrackH = 6, MarkerH = 26;

    private readonly AppSettings _settings;
    private readonly string _sourcePath;
    private readonly int _fps;
    private readonly int _videoW, _videoH;    // physical px
    private readonly Action<string>? _onSaved;

    private readonly Border _videoBorder;
    private readonly MediaElement _player = new();
    private readonly TextBlock _playGlyph;
    private readonly TextBlock _timeText;
    private readonly Canvas _timeline = new();
    private readonly Rectangle _track = new(), _range = new(), _marker = new();
    private readonly Border _startHandle, _endHandle;
    private readonly ComboBox _format;
    private readonly StackPanel _actions;
    private readonly Grid _progressRow;
    private readonly ProgressBar _progressBar = new();
    private readonly TextBlock _progressText = new();
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(80) };

    private double _duration;                 // seconds; 0 until MediaOpened
    private double _trimStart, _trimEnd;
    private bool _playing, _saved, _exporting;
    private CancellationTokenSource? _exportCts;

    private enum DragTarget { None, Start, End, Playhead }
    private DragTarget _drag;

    // ---- annotations (authored in video-pixel space; static across the clip) ----
    private enum Tool { None, Text, Shape, Line, Blur, Mosaic, Sticker }
    private Tool _tool = Tool.None;
    private readonly Canvas _blurHost = new();   // blur previews (exported via ffmpeg filters)
    private readonly Canvas _annoHost = new();   // text/shapes/lines/stickers (exported as a PNG overlay)
    private readonly Canvas _interact = new();   // captures clicks/drags for the active tool
    private readonly EditHistory _history = new();
    private readonly List<Button> _toolButtons = new();
    private readonly List<(Rectangle Visual, BlurRegion Region)> _blurs = new();
    private StackPanel _toolsPanel = null!;
    private Button _undoButton = null!, _redoButton = null!;
    private KeyGesture? _undoGesture, _redoGesture, _copyGesture;
    private TextBox? _editingText;
    private Point _toolDragStart;
    private Shape? _shapePreview;
    private Rectangle? _regionPreview;
    private ShapesPath? _linePreview;
    private Image? _stickerDrag;
    private Point _stickerGrab;

    // Per-tool settings (loaded from AppSettings, persisted back on export)
    private Color _textColor, _shapeColor, _lineColor;
    private string _textFont = "Segoe UI";
    private double _textSize = 24;
    private bool _textBold, _textItalic, _textStrike;
    private ShapeKind _shapeKind;
    private bool _shapeFilled;
    private double _shapeWidth;
    private double _lineWidth = 3;
    private bool _lineArrowStart, _lineArrowEnd;
    private double _gaussStrength = 12, _mosaicStrength = 12;
    private readonly List<Color> _customColors = new();
    private readonly List<Button> _customSwatchButtons = new();
    private const int MaxCustomColors = 8;

    // Direct manipulation (hover-grab any annotation, Delete removes)
    private readonly Canvas _selectionLayer = new();   // dashed selection box overlay
    private Grid _pixelStack = null!;
    private FrameworkElement? _selected;
    private Rectangle? _selectionBox;
    private bool _moveDragging;
    private Point _moveGrab;
    private double _moveOrigX, _moveOrigY;

    // Tool options UI (one row per tool below the transport row)
    private StackPanel _textOptions = null!, _shapeOptions = null!, _lineOptions = null!,
        _blurOptions = null!, _stickerOptions = null!;
    private ComboBox _fontCombo = null!;
    private Slider _textSizeSlider = null!, _shapeWidthSlider = null!, _lineWidthSlider = null!, _blurSlider = null!;
    private Button _boldButton = null!, _italicButton = null!, _strikeButton = null!;
    private Button _arrowStartButton = null!, _arrowEndButton = null!;
    private readonly List<Button> _shapeKindButtons = new();
    private readonly List<Button> _shapeStyleButtons = new();
    private bool _blurSliderSync;   // true while SelectTool loads the slider value

    public RecordingEditorWindow(string sourcePath, int videoWidth, int videoHeight, int fps,
                                 AppSettings settings, Action<string>? onSaved)
    {
        _sourcePath = sourcePath;
        _videoW = videoWidth;
        _videoH = videoHeight;
        _fps = Math.Max(1, fps);
        _settings = settings;
        _onSaved = onSaved;

        // Annotation defaults are shared with the capture editor's persisted settings.
        _textColor = ParseColor(settings.TextColor, Colors.Red);
        _textFont = settings.TextFont;
        _textSize = settings.TextSize;
        _textBold = settings.TextBold;
        _textItalic = settings.TextItalic;
        _textStrike = settings.TextStrikethrough;
        _shapeColor = ParseColor(settings.ShapeColor, Colors.Red);
        _shapeKind = Enum.TryParse<ShapeKind>(settings.ShapeKind, out var sk) ? sk : ShapeKind.Rectangle;
        _shapeFilled = settings.ShapeFilled;
        _shapeWidth = settings.ShapeWidth;
        _lineColor = ParseColor(settings.LineColor, Colors.Red);
        _lineWidth = settings.LineWidth;
        _lineArrowStart = settings.LineArrowStart;
        _lineArrowEnd = settings.LineArrowEnd;
        _gaussStrength = settings.GaussianStrength;
        _mosaicStrength = settings.MosaicStrength;
        _undoGesture = HotkeyGesture.Parse(settings.UndoHotkey);
        _redoGesture = HotkeyGesture.Parse(settings.RedoHotkey);
        _copyGesture = HotkeyGesture.Parse(settings.CopyHotkey);
        foreach (var hex in settings.CustomColors)
        {
            try { _customColors.Add((Color)ColorConverter.ConvertFromString(hex)); }
            catch { /* skip invalid entries */ }
            if (_customColors.Count >= MaxCustomColors) break;
        }

        Title = Loc.T("edit.title");
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        // Positioned manually in OnSourceInitialized: CenterScreen would run BEFORE the
        // preview is sized to the clip, centering a tiny window and letting the final
        // (grown) window spill off-screen.
        WindowStartupLocation = WindowStartupLocation.Manual;

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // video
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // transport + tools
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // tool options
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // timeline
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // hint
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // actions / progress

        // ---- video preview + annotation layers (authored in video-pixel space; the
        //      Viewbox scales the whole stack uniformly for display) ----
        _player.LoadedBehavior = MediaState.Manual;
        _player.UnloadedBehavior = MediaState.Manual;
        _player.ScrubbingEnabled = true;      // show the frame while paused/seeking
        _player.Stretch = Stretch.Uniform;
        _player.Width = _videoW;
        _player.Height = _videoH;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += (_, _) => LoopToStart();

        foreach (var host in new[] { _blurHost, _annoHost, _interact })
        {
            host.Width = _videoW;
            host.Height = _videoH;
            host.ClipToBounds = true;
        }
        _interact.Background = Brushes.Transparent;   // hit-test the full area when a tool is active
        _interact.IsHitTestVisible = false;
        _interact.MouseLeftButtonDown += Interact_MouseDown;
        _interact.MouseMove += Interact_MouseMove;
        _interact.MouseLeftButtonUp += Interact_MouseUp;

        _selectionLayer.Width = _videoW;
        _selectionLayer.Height = _videoH;
        _selectionLayer.IsHitTestVisible = false;

        _pixelStack = new Grid { Width = _videoW, Height = _videoH };
        _pixelStack.Children.Add(_player);
        _pixelStack.Children.Add(_blurHost);
        _pixelStack.Children.Add(_annoHost);
        _pixelStack.Children.Add(_selectionLayer);
        _pixelStack.Children.Add(_interact);
        // Clicking the video outside the active text box commits it, like Enter
        // (tool/option clicks elsewhere in the window keep the box editable).
        _pixelStack.PreviewMouseLeftButtonDown += (_, pe) =>
        {
            if (_editingText is { } box && pe.OriginalSource is DependencyObject src && !IsWithin(src, box))
                CommitActiveText();
        };
        // Direct manipulation: tunneling handlers grab annotations before the
        // interaction layer sees the click — drawing only starts on empty space.
        _pixelStack.PreviewMouseLeftButtonDown += Stack_PreviewMouseDown;
        _pixelStack.PreviewMouseMove += Stack_PreviewMouseMove;
        _pixelStack.PreviewMouseLeftButtonUp += Stack_PreviewMouseUp;
        var pixelStack = _pixelStack;

        _videoBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
            BorderBrush = Theme.ControlBorderBrush,
            BorderThickness = new Thickness(1),
            Child = new Viewbox { Child = pixelStack, Stretch = Stretch.Uniform },
            MinWidth = 420,
            MinHeight = 200,
        };
        Grid.SetRow(_videoBorder, 0);
        root.Children.Add(_videoBorder);

        // ---- transport: play/pause + time readout ----
        _playGlyph = new TextBlock
        {
            Text = GlyphPlay,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var playButton = new Button
        {
            Content = _playGlyph,
            Width = 34,
            Height = 28,
            Background = Theme.ButtonBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            Cursor = Cursors.Hand,
        };
        playButton.Click += (_, _) => TogglePlay();

        _timeText = new TextBlock
        {
            Text = "00:00.0 / 00:00.0",
            Foreground = Theme.ForegroundBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 13,
        };
        var transport = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        transport.Children.Add(playButton);
        transport.Children.Add(_timeText);

        // ---- annotation tools ----
        _toolsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0) };
        _toolsPanel.Children.Add(MakeToolButton(GlyphText, Loc.T("tool.text"), Tool.Text));
        _toolsPanel.Children.Add(MakeToolButton(GlyphShape, Loc.T("tool.shape"), Tool.Shape));
        _toolsPanel.Children.Add(MakeToolButton(GlyphLine, Loc.T("tool.line"), Tool.Line));
        _toolsPanel.Children.Add(MakeToolButton(GlyphSticker, Loc.T("tool.sticker"), Tool.Sticker));
        _toolsPanel.Children.Add(MakeToolTextButton(Loc.T("lbl.gaussian"), Tool.Blur));
        _toolsPanel.Children.Add(MakeToolTextButton(Loc.T("lbl.mosaic"), Tool.Mosaic));
        _undoButton = MakeIconButton(GlyphUndo, Loc.T("action.undo") + " (" + _settings.UndoHotkey + ")", () => _history.Undo());
        _redoButton = MakeIconButton(GlyphRedo, Loc.T("action.redo") + " (" + _settings.RedoHotkey + ")", () => _history.Redo());
        _undoButton.Margin = new Thickness(10, 0, 2, 0);
        _undoButton.IsEnabled = _redoButton.IsEnabled = false;
        _toolsPanel.Children.Add(_undoButton);
        _toolsPanel.Children.Add(_redoButton);
        transport.Children.Add(_toolsPanel);
        _history.Changed += () =>
        {
            _undoButton.IsEnabled = _history.CanUndo;
            _redoButton.IsEnabled = _history.CanRedo;
            // Undo/redo may have removed, re-added, or repositioned the selection.
            if (_selected != null) UpdateSelectionBox();
        };

        Grid.SetRow(transport, 1);
        root.Children.Add(transport);

        // ---- tool options (one row per tool, mirroring the capture toolbar) ----
        var optionsHost = new StackPanel();
        BuildOptionPanels(optionsHost);
        Grid.SetRow(optionsHost, 2);
        root.Children.Add(optionsHost);

        // ---- timeline (track + trim range + handles + playhead) ----
        _timeline.Height = TimelineH;
        _timeline.Margin = new Thickness(0, 8, 0, 0);
        _timeline.Background = Brushes.Transparent;   // hit-test the whole strip

        _track.Height = TrackH;
        _track.RadiusX = _track.RadiusY = TrackH / 2;
        _track.Fill = Theme.ControlBgBrush;
        _timeline.Children.Add(_track);

        _range.Height = TrackH;
        _range.Fill = new SolidColorBrush(Color.FromArgb(0x88, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
        _timeline.Children.Add(_range);

        _startHandle = MakeHandle();
        _endHandle = MakeHandle();
        _timeline.Children.Add(_startHandle);
        _timeline.Children.Add(_endHandle);

        _marker.Width = 2;
        _marker.Height = MarkerH;
        _marker.Fill = Theme.ForegroundBrush;
        _marker.IsHitTestVisible = false;
        _timeline.Children.Add(_marker);

        _timeline.MouseLeftButtonDown += Timeline_MouseDown;
        _timeline.MouseMove += Timeline_MouseMove;
        _timeline.MouseLeftButtonUp += Timeline_MouseUp;
        _timeline.SizeChanged += (_, _) => LayoutTimeline();
        Grid.SetRow(_timeline, 3);
        root.Children.Add(_timeline);

        var hint = new TextBlock
        {
            Text = Loc.T("edit.hint"),
            Foreground = Theme.ForegroundBrush,
            Opacity = 0.6,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetRow(hint, 4);
        root.Children.Add(hint);

        // ---- bottom: format + actions, swapped for a progress row while exporting ----
        var bottom = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var formatPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        formatPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("set.recordFormat"),
            Foreground = Theme.ForegroundBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        _format = new ComboBox
        {
            Width = 84,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Theme.ControlBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ControlBorderBrush,
        };
        foreach (var (token, label) in new[] { ("gif", "GIF"), ("mp4", "MP4"), ("webp", "WebP") })
        {
            var item = new ComboBoxItem { Content = label, Tag = token };
            _format.Items.Add(item);
            if (token.Equals(_settings.RecordFormat, StringComparison.OrdinalIgnoreCase))
                _format.SelectedItem = item;
        }
        if (_format.SelectedItem == null) _format.SelectedIndex = 0;
        formatPanel.Children.Add(_format);
        Grid.SetColumn(formatPanel, 0);
        bottom.Children.Add(formatPanel);

        _actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _actions.Children.Add(ActionButton(Loc.T("common.save"), () => Export(quick: false), isDefault: true));
        _actions.Children.Add(ActionButton(Loc.T("set.quickSave"), () => Export(quick: true)));
        _actions.Children.Add(ActionButton(Loc.T("common.cancel"), Close));
        Grid.SetColumn(_actions, 1);
        bottom.Children.Add(_actions);

        _progressRow = new Grid { Visibility = Visibility.Collapsed };
        _progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _progressBar.Height = 8;
        _progressBar.Maximum = 100;
        _progressBar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_progressBar, 0);
        _progressRow.Children.Add(_progressBar);
        _progressText.Foreground = Theme.ForegroundBrush;
        _progressText.VerticalAlignment = VerticalAlignment.Center;
        _progressText.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(_progressText, 1);
        _progressRow.Children.Add(_progressText);
        var cancelExport = ActionButton(Loc.T("common.cancel"), () => _exportCts?.Cancel());
        Grid.SetColumn(cancelExport, 2);
        _progressRow.Children.Add(cancelExport);
        Grid.SetColumn(_progressRow, 1);
        bottom.Children.Add(_progressRow);

        Grid.SetRow(bottom, 5);
        root.Children.Add(bottom);

        Content = root;

        KeyDown += OnKeyDown;
        SourceInitialized += OnSourceInitialized;
        _ticker.Tick += (_, _) => OnTick();

        _player.Source = new Uri(_sourcePath);
        _player.Play();   // must start once so MediaOpened fires; paused again on open
    }

    // Segoe MDL2 Assets
    private static readonly string GlyphPlay = char.ConvertFromUtf32(0xE768);
    private static readonly string GlyphPause = char.ConvertFromUtf32(0xE769);
    private static readonly string GlyphText = char.ConvertFromUtf32(0xE8D2);     // Font (A)
    private static readonly string GlyphShape = char.ConvertFromUtf32(0xE71A);    // Stop (square)
    private static readonly string GlyphLine = char.ConvertFromUtf32(0xE72A);     // Forward (arrow)
    private static readonly string GlyphSticker = char.ConvertFromUtf32(0xE8B9);  // Pictures
    private static readonly string GlyphUndo = char.ConvertFromUtf32(0xE7A7);
    private static readonly string GlyphRedo = char.ConvertFromUtf32(0xE7A6);

    // ---------------------------------------------------------------- setup ---

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Size the preview to the clip's physical pixels (capped to the work area of the
        // monitor the user is on), then center the finished window there manually.
        var hwnd = new WindowInteropHelper(this).Handle;
        uint dpiRaw = NativeMethods.GetDpiForWindow(hwnd);
        double dpi = dpiRaw <= 0 ? 1.0 : dpiRaw / 96.0;

        var wa = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;   // physical px

        double dispW = _videoW / dpi;
        double dispH = _videoH / dpi;
        double maxW = Math.Max(420, wa.Width / dpi * 0.85 - 28);
        double maxH = Math.Max(240, wa.Height / dpi * 0.85 - 190);   // leave room for controls
        double s = Math.Min(1.0, Math.Min(maxW / dispW, maxH / dispH));

        _videoBorder.Width = Math.Max(_videoBorder.MinWidth, dispW * s);
        _videoBorder.Height = Math.Max(_videoBorder.MinHeight, dispH * s);

        // Force the SizeToContent pass so ActualWidth/Height reflect the grown preview,
        // then center within the work area (clamped so the title bar stays reachable).
        UpdateLayout();
        int winW = (int)Math.Round(ActualWidth * dpi);
        int winH = (int)Math.Round(ActualHeight * dpi);
        int x = wa.Left + Math.Max(0, (wa.Width - winW) / 2);
        int y = wa.Top + Math.Max(0, (wa.Height - winH) / 2);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
    }

    private void OnMediaOpened(object? sender, RoutedEventArgs e)
    {
        _duration = _player.NaturalDuration.HasTimeSpan
            ? _player.NaturalDuration.TimeSpan.TotalSeconds : 0;
        _trimStart = 0;
        _trimEnd = _duration;
        LayoutTimeline();
        SetPlaying(true);   // auto-play the loop so the user immediately sees the take
    }

    /// <summary>Smallest allowed trimmed length: two frames (or 0.15 s, whichever is larger).</summary>
    private double MinTrimGap => Math.Max(0.15, 2.0 / _fps);

    // ------------------------------------------------------------- playback ---

    private void TogglePlay() => SetPlaying(!_playing);

    private void SetPlaying(bool play)
    {
        if (_exporting) return;
        _playing = play;
        _playGlyph.Text = play ? GlyphPause : GlyphPlay;
        if (play)
        {
            double pos = _player.Position.TotalSeconds;
            if (pos < _trimStart || pos >= _trimEnd - 0.02)
                _player.Position = TimeSpan.FromSeconds(_trimStart);
            _player.Play();
            _ticker.Start();
        }
        else
        {
            _player.Pause();
            _ticker.Stop();
            UpdatePlayhead(_player.Position.TotalSeconds);
        }
    }

    private void LoopToStart()
    {
        _player.Position = TimeSpan.FromSeconds(_trimStart);
        if (_playing) _player.Play();
    }

    private void OnTick()
    {
        double pos = _player.Position.TotalSeconds;
        if (_playing && pos >= _trimEnd - 0.02) { LoopToStart(); return; }
        UpdatePlayhead(pos);
    }

    private void Seek(double t)
    {
        t = Math.Clamp(t, 0, Math.Max(0, _duration));
        _player.Position = TimeSpan.FromSeconds(t);
        UpdatePlayhead(t);
    }

    // ------------------------------------------------------------- timeline ---

    private Border MakeHandle() => new()
    {
        Width = HandleW,
        Height = HandleH,
        CornerRadius = new CornerRadius(3),
        Background = new SolidColorBrush(Theme.Accent),
        Cursor = Cursors.SizeWE,
        IsHitTestVisible = false,   // drags are resolved by the canvas for a larger hit area
    };

    private double TimeToX(double t) => _duration <= 0 ? 0 : t / _duration * _timeline.ActualWidth;
    private double XToTime(double x) => _duration <= 0 ? 0
        : Math.Clamp(x / Math.Max(1, _timeline.ActualWidth), 0, 1) * _duration;

    private void LayoutTimeline()
    {
        double w = _timeline.ActualWidth;
        if (w <= 0) return;

        double midY = TimelineH / 2;
        _track.Width = w;
        Canvas.SetLeft(_track, 0);
        Canvas.SetTop(_track, midY - TrackH / 2);

        double x1 = TimeToX(_trimStart), x2 = TimeToX(_trimEnd);
        _range.Width = Math.Max(0, x2 - x1);
        Canvas.SetLeft(_range, x1);
        Canvas.SetTop(_range, midY - TrackH / 2);

        Canvas.SetLeft(_startHandle, x1 - HandleW / 2);
        Canvas.SetTop(_startHandle, midY - HandleH / 2);
        Canvas.SetLeft(_endHandle, x2 - HandleW / 2);
        Canvas.SetTop(_endHandle, midY - HandleH / 2);

        UpdatePlayhead(_player.Position.TotalSeconds);
    }

    private void UpdatePlayhead(double t)
    {
        Canvas.SetLeft(_marker, TimeToX(t) - 1);
        Canvas.SetTop(_marker, TimelineH / 2 - MarkerH / 2);
        _timeText.Text = $"{FormatTime(t)} / {FormatTime(_trimEnd - _trimStart)}";
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        int m = (int)(seconds / 60);
        return $"{m:00}:{seconds - m * 60:00.0}";
    }

    private void Timeline_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_exporting || _duration <= 0) return;
        double x = e.GetPosition(_timeline).X;

        double xs = TimeToX(_trimStart), xe = TimeToX(_trimEnd);
        if (Math.Abs(x - xs) <= HandleHitPx) _drag = DragTarget.Start;
        else if (Math.Abs(x - xe) <= HandleHitPx) _drag = DragTarget.End;
        else { _drag = DragTarget.Playhead; SetPlaying(false); Seek(XToTime(x)); }

        _timeline.CaptureMouse();
        e.Handled = true;
    }

    private void Timeline_MouseMove(object sender, MouseEventArgs e)
    {
        double x = e.GetPosition(_timeline).X;

        if (_drag == DragTarget.None)
        {
            // Hover feedback: resize cursor near a trim handle.
            double xs = TimeToX(_trimStart), xe = TimeToX(_trimEnd);
            _timeline.Cursor = (Math.Abs(x - xs) <= HandleHitPx || Math.Abs(x - xe) <= HandleHitPx)
                ? Cursors.SizeWE : Cursors.Arrow;
            return;
        }

        double t = XToTime(x);
        switch (_drag)
        {
            case DragTarget.Start:
                _trimStart = Math.Clamp(t, 0, Math.Max(0, _trimEnd - MinTrimGap));
                SetPlaying(false);
                Seek(_trimStart);      // scrub the frame under the handle
                break;
            case DragTarget.End:
                _trimEnd = Math.Clamp(t, Math.Min(_duration, _trimStart + MinTrimGap), _duration);
                SetPlaying(false);
                Seek(_trimEnd);
                break;
            case DragTarget.Playhead:
                Seek(t);
                break;
        }
        LayoutTimeline();
    }

    private void Timeline_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _drag = DragTarget.None;
        _timeline.ReleaseMouseCapture();
    }

    // ---------------------------------------------------- annotation tools ---

    private void SelectTool(Tool t)
    {
        if (_exporting) return;
        CommitActiveText();
        _tool = _tool == t ? Tool.None : t;   // click the active tool again to deselect
        foreach (var b in _toolButtons)
            b.Background = (Tool)b.Tag! == _tool && _tool != Tool.None ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _interact.IsHitTestVisible = ToolNeedsInteract(_tool);

        _textOptions.Visibility = _tool == Tool.Text ? Visibility.Visible : Visibility.Collapsed;
        _shapeOptions.Visibility = _tool == Tool.Shape ? Visibility.Visible : Visibility.Collapsed;
        _lineOptions.Visibility = _tool == Tool.Line ? Visibility.Visible : Visibility.Collapsed;
        _blurOptions.Visibility = _tool is Tool.Blur or Tool.Mosaic ? Visibility.Visible : Visibility.Collapsed;
        _stickerOptions.Visibility = _tool == Tool.Sticker ? Visibility.Visible : Visibility.Collapsed;

        if (_tool is Tool.Blur or Tool.Mosaic)
        {
            _blurSliderSync = true;
            _blurSlider.Value = _tool == Tool.Blur ? _gaussStrength : _mosaicStrength;
            _blurSliderSync = false;
        }
        if (_tool == Tool.Shape) { RefreshShapeKindButtons(); RefreshShapeStyleButtons(); }
        if (_tool == Tool.Line) RefreshArrowToggles();
        RefreshSwatchSelection();

        if (_tool == Tool.Sticker && _annoHost.Children.OfType<Image>().Any() == false) AddSticker();
    }

    private static bool ToolNeedsInteract(Tool t) =>
        t is Tool.Text or Tool.Shape or Tool.Line or Tool.Blur or Tool.Mosaic;

    private void Interact_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_exporting) return;
        var p = e.GetPosition(_interact);
        switch (_tool)
        {
            case Tool.Text:
                PlaceText(p);
                e.Handled = true;
                return;
            case Tool.Shape:
                BeginShape(p);
                break;
            case Tool.Line:
                BeginLine(p);
                break;
            case Tool.Blur or Tool.Mosaic:
                BeginRegion(p);
                break;
            default:
                return;
        }
        _interact.CaptureMouse();
        e.Handled = true;
    }

    private void Interact_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(_interact);
        if (_linePreview != null)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) p = ArrowGeometry.SnapTo45(_toolDragStart, p);
            _linePreview.Data = ArrowGeometry.Build(_toolDragStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);
        }
        else if (_shapePreview != null)
        {
            var r = MakeRect(_toolDragStart, p);
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
        else if (_regionPreview != null)
        {
            var r = MakeRect(_toolDragStart, p);
            Canvas.SetLeft(_regionPreview, r.X);
            Canvas.SetTop(_regionPreview, r.Y);
            _regionPreview.Width = r.Width;
            _regionPreview.Height = r.Height;
        }
    }

    private void Interact_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_interact);
        _interact.ReleaseMouseCapture();

        if (_linePreview is { } line)
        {
            _linePreview = null;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) p = ArrowGeometry.SnapTo45(_toolDragStart, p);
            if ((p - _toolDragStart).Length < 8) { _annoHost.Children.Remove(line); return; }
            line.Data = ArrowGeometry.Build(_toolDragStart, p, _lineWidth, _lineArrowStart, _lineArrowEnd);
            _history.Push(
                undo: () => _annoHost.Children.Remove(line),
                redo: () => { if (!_annoHost.Children.Contains(line)) _annoHost.Children.Add(line); });
            return;
        }

        if (_shapePreview is { } shape)
        {
            _shapePreview = null;
            var r = MakeRect(_toolDragStart, p);
            if (r.Width < 4 || r.Height < 4) { _annoHost.Children.Remove(shape); return; }
            _history.Push(
                undo: () => _annoHost.Children.Remove(shape),
                redo: () => { if (!_annoHost.Children.Contains(shape)) _annoHost.Children.Add(shape); });
        }
        else if (_regionPreview is { } preview)
        {
            _regionPreview = null;
            _interact.Children.Remove(preview);

            var r = MakeRect(_toolDragStart, p);
            r.Intersect(new Rect(0, 0, _videoW, _videoH));
            if (r.IsEmpty || r.Width < 6 || r.Height < 6) return;
            AddBlur(new Int32Rect(
                (int)Math.Round(r.X), (int)Math.Round(r.Y),
                Math.Max(2, (int)Math.Round(r.Width)), Math.Max(2, (int)Math.Round(r.Height))),
                mosaic: _tool == Tool.Mosaic);
        }
    }

    private void BeginShape(Point start)
    {
        _toolDragStart = start;
        var brush = new SolidColorBrush(_shapeColor);
        Shape s = _shapeKind == ShapeKind.Ellipse ? new Ellipse() : new Rectangle();
        if (_shapeFilled) s.Fill = brush;
        else { s.Fill = Brushes.Transparent; s.Stroke = brush; s.StrokeThickness = _shapeWidth; }
        Canvas.SetLeft(s, start.X);
        Canvas.SetTop(s, start.Y);
        _annoHost.Children.Add(s);
        _shapePreview = s;
    }

    private void BeginLine(Point start)
    {
        _toolDragStart = start;
        var brush = new SolidColorBrush(_lineColor);
        _linePreview = new ShapesPath
        {
            Stroke = brush,
            Fill = brush,                    // fills the arrowhead triangles
            StrokeThickness = _lineWidth,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        _annoHost.Children.Add(_linePreview);
    }

    private void BeginRegion(Point start)
    {
        _toolDragStart = start;
        _regionPreview = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0xA9, 0xFC)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x3D, 0xA9, 0xFC)),
        };
        Canvas.SetLeft(_regionPreview, start.X);
        Canvas.SetTop(_regionPreview, start.Y);
        _interact.Children.Add(_regionPreview);   // lives outside _annoHost so it never exports
    }

    /// <summary>
    /// Register a blur region and show a live preview: the region of the playing video,
    /// re-painted via VisualBrush with a BlurEffect. Export applies the real ffmpeg filter.
    /// </summary>
    private void AddBlur(Int32Rect r, bool mosaic)
    {
        double strength = mosaic ? _mosaicStrength : _gaussStrength;
        var visual = new Rectangle
        {
            Width = r.Width,
            Height = r.Height,
            IsHitTestVisible = false,
            Fill = new VisualBrush(_player)
            {
                Viewbox = new Rect(r.X, r.Y, r.Width, r.Height),
                ViewboxUnits = BrushMappingMode.Absolute,
            },
            Effect = new BlurEffect { Radius = Math.Max(4, strength) },
        };
        Canvas.SetLeft(visual, r.X);
        Canvas.SetTop(visual, r.Y);

        var entry = (Visual: visual, Region: new BlurRegion(r, mosaic, strength));
        _blurHost.Children.Add(visual);
        _blurs.Add(entry);
        _history.Push(
            undo: () => { _blurHost.Children.Remove(visual); _blurs.Remove(entry); },
            redo: () =>
            {
                if (_blurHost.Children.Contains(visual)) return;
                _blurHost.Children.Add(visual);
                _blurs.Add(entry);
            });
    }

    private void PlaceText(Point p)
    {
        CommitActiveText();

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
        Canvas.SetLeft(box, p.X);
        Canvas.SetTop(box, p.Y);
        _annoHost.Children.Add(box);
        _editingText = box;
        ApplyTextStyle();
        _interact.IsHitTestVisible = false;   // let the box receive clicks/typing

        box.PreviewKeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape ||
                (ke.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0))
            {
                CommitActiveText();
                ke.Handled = true;
            }
        };
        box.Loaded += (_, _) => { box.Focus(); Keyboard.Focus(box); };
        box.Focus();
        Keyboard.Focus(box);
    }

    /// <summary>Finalize the text being edited: bake it into a static label or discard if empty.</summary>
    private void CommitActiveText()
    {
        var box = _editingText;
        if (box == null) return;
        _editingText = null;

        if (string.IsNullOrWhiteSpace(box.Text))
        {
            _annoHost.Children.Remove(box);
        }
        else
        {
            box.IsReadOnly = true;
            box.IsHitTestVisible = false;
            box.Focusable = false;
            box.BorderThickness = new Thickness(0);
            box.Background = Brushes.Transparent;
            box.CaretBrush = Brushes.Transparent;
            _history.Push(
                undo: () => _annoHost.Children.Remove(box),
                redo: () => { if (!_annoHost.Children.Contains(box)) _annoHost.Children.Add(box); });
        }

        _interact.IsHitTestVisible = ToolNeedsInteract(_tool);
    }

    private void AddSticker()
    {
        SetPlaying(false);
        var dlg = new OpenFileDialog { Title = Loc.T("img.chooseTitle"), Filter = Loc.T("img.filter") };
        if (dlg.ShowDialog(this) != true) return;

        var src = ImageLoader.TryLoad(dlg.FileName);
        if (src == null)
        {
            MessageBox.Show(this, Loc.T("img.loadFail"), "ScreenPaste",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Fit within ~60% of the video while keeping aspect ratio.
        double scale = Math.Min(1.0, Math.Min(
            _videoW * 0.6 / src.PixelWidth, _videoH * 0.6 / src.PixelHeight));
        double w = Math.Max(16, src.PixelWidth * scale);
        double h = Math.Max(16, src.PixelHeight * scale);

        var image = new Image { Source = src, Width = w, Height = h, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        Canvas.SetLeft(image, (_videoW - w) / 2);
        Canvas.SetTop(image, (_videoH - h) / 2);
        AttachStickerInteractions(image);
        _annoHost.Children.Add(image);

        _history.Push(
            undo: () => _annoHost.Children.Remove(image),
            redo: () => { if (!_annoHost.Children.Contains(image)) _annoHost.Children.Add(image); });
    }

    private void AttachStickerInteractions(Image image)
    {
        image.Cursor = Cursors.SizeAll;

        image.MouseLeftButtonDown += (_, e) =>
        {
            if (_exporting) return;
            _stickerDrag = image;
            var p = e.GetPosition(_annoHost);
            _stickerGrab = new Point(p.X - Canvas.GetLeft(image), p.Y - Canvas.GetTop(image));
            image.CaptureMouse();
            e.Handled = true;
        };
        image.MouseMove += (_, e) =>
        {
            if (_stickerDrag != image) return;
            var p = e.GetPosition(_annoHost);
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
            if (_exporting) return;
            double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            double nw = Math.Max(16, image.Width * factor);
            double nh = Math.Max(16, image.Height * factor);
            Canvas.SetLeft(image, Canvas.GetLeft(image) - (nw - image.Width) / 2);
            Canvas.SetTop(image, Canvas.GetTop(image) - (nh - image.Height) / 2);
            image.Width = nw;
            image.Height = nh;
            e.Handled = true;
        };
    }

    // -------------------------------------------- direct manipulation ---
    // Hover any annotation (shape / line / text / sticker) with ANY tool: dragging
    // grabs and moves it, Delete removes it, both undoable. Bounding-box hit testing
    // over _annoHost; moves use a TranslateTransform. Blur regions stay undo-only.

    private void Stack_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_exporting || _editingText != null) return;
        var p = e.GetPosition(_annoHost);
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
        _pixelStack.CaptureMouse();
        e.Handled = true;
    }

    private void Stack_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(_annoHost);

        if (_moveDragging && _selected != null)
        {
            var tt = EnsureTranslate(_selected);
            tt.X = _moveOrigX + (p.X - _moveGrab.X);
            tt.Y = _moveOrigY + (p.Y - _moveGrab.Y);
            UpdateSelectionBox();
            e.Handled = true;
            return;
        }

        if (_editingText == null && !_exporting && e.LeftButton == MouseButtonState.Released)
            _pixelStack.Cursor = HitAnnotation(p) != null ? Cursors.SizeAll : null;
    }

    private void Stack_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_moveDragging) return;
        _moveDragging = false;
        _pixelStack.ReleaseMouseCapture();
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

    private FrameworkElement? HitAnnotation(Point p)
    {
        for (int i = _annoHost.Children.Count - 1; i >= 0; i--)
            if (_annoHost.Children[i] is FrameworkElement el && AnnotationBounds(el).Contains(p))
                return el;
        return null;
    }

    private static Rect AnnotationBounds(FrameworkElement el)
    {
        Rect r;
        if (el is ShapesPath { Data: { } g } path)
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
            _selectionLayer.Children.Add(_selectionBox);
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

    private static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

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

    // ------------------------------------------------------- tool options UI ---
    // Ported from the capture toolbar: per-tool option rows with sliders (live numeric
    // readouts), style toggles, and the shared colour palette (persisted custom colours,
    // selected-swatch outline, right-click removal, "+" opens the full picker).

    private static readonly Color[] Palette =
    {
        Colors.Red, Colors.Orange, Color.FromRgb(0xFF, 0xEB, 0x3B),
        Colors.LimeGreen, Color.FromRgb(0x3D, 0xA9, 0xFC), Colors.Black, Colors.White,
    };
    private static readonly SolidColorBrush SwatchBorder = new(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));

    private void BuildOptionPanels(StackPanel host)
    {
        // ---- Text: font / size / style / colour ----
        _textOptions = NewOptionsRow();
        _textOptions.Children.Add(OptLabel(Loc.T("lbl.font")));
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
        _textOptions.Children.Add(_fontCombo);
        _textOptions.Children.Add(OptLabel(Loc.T("lbl.size")));
        _textSizeSlider = new Slider { Minimum = 10, Maximum = 96, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _textSize };
        _textSizeSlider.ValueChanged += (_, se) => { _textSize = se.NewValue; ApplyTextStyle(); };
        _textOptions.Children.Add(_textSizeSlider);
        _textOptions.Children.Add(ValueReadout(_textSizeSlider, v => v.ToString("0")));
        _textOptions.Children.Add(OptLabel(Loc.T("lbl.style")));
        _boldButton = MakeStyleToggle("B", () => { _textBold = !_textBold; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _italicButton = MakeStyleToggle("I", () => { _textItalic = !_textItalic; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _strikeButton = MakeStyleToggle("S", () => { _textStrike = !_textStrike; RefreshStyleToggles(); ApplyTextStyle(); RefocusText(); });
        _textOptions.Children.Add(_boldButton);
        _textOptions.Children.Add(_italicButton);
        _textOptions.Children.Add(_strikeButton);
        _textOptions.Children.Add(OptLabel(Loc.T("lbl.color")));
        AddColorSwatches(_textOptions);
        RefreshStyleToggles();
        host.Children.Add(_textOptions);

        // ---- Shape: kind / style / thickness / colour ----
        _shapeOptions = NewOptionsRow();
        _shapeOptions.Children.Add(OptLabel(Loc.T("lbl.shape")));
        _shapeOptions.Children.Add(MakeShapeKindButton(Loc.T("shape.rect"), ShapeKind.Rectangle));
        _shapeOptions.Children.Add(MakeShapeKindButton(Loc.T("shape.rounded"), ShapeKind.RoundedRectangle));
        _shapeOptions.Children.Add(MakeShapeKindButton(Loc.T("shape.ellipse"), ShapeKind.Ellipse));
        _shapeOptions.Children.Add(OptLabel(Loc.T("lbl.style")));
        _shapeOptions.Children.Add(MakeShapeStyleButton(Loc.T("style.outline"), filled: false));
        _shapeOptions.Children.Add(MakeShapeStyleButton(Loc.T("style.fill"), filled: true));
        _shapeOptions.Children.Add(OptLabel(Loc.T("lbl.lineWidth")));
        _shapeWidthSlider = new Slider { Minimum = 1, Maximum = 20, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _shapeWidth };
        _shapeWidthSlider.ValueChanged += (_, se) => _shapeWidth = se.NewValue;
        _shapeOptions.Children.Add(_shapeWidthSlider);
        _shapeOptions.Children.Add(ValueReadout(_shapeWidthSlider, v => v.ToString("0")));
        _shapeOptions.Children.Add(OptLabel(Loc.T("lbl.color")));
        AddColorSwatches(_shapeOptions);
        host.Children.Add(_shapeOptions);

        // ---- Line: thickness / arrowheads / colour ----
        _lineOptions = NewOptionsRow();
        _lineOptions.Children.Add(OptLabel(Loc.T("lbl.lineWidth")));
        _lineWidthSlider = new Slider { Minimum = 1, Maximum = 20, Width = 90, VerticalAlignment = VerticalAlignment.Center, Value = _lineWidth };
        _lineWidthSlider.ValueChanged += (_, se) => _lineWidth = se.NewValue;
        _lineOptions.Children.Add(_lineWidthSlider);
        _lineOptions.Children.Add(ValueReadout(_lineWidthSlider, v => v.ToString("0")));
        _arrowStartButton = MakeSmallToggle(Loc.T("line.arrowStart"));
        _arrowStartButton.Click += (_, _) => { _lineArrowStart = !_lineArrowStart; RefreshArrowToggles(); };
        _arrowEndButton = MakeSmallToggle(Loc.T("line.arrowEnd"));
        _arrowEndButton.Click += (_, _) => { _lineArrowEnd = !_lineArrowEnd; RefreshArrowToggles(); };
        _lineOptions.Children.Add(_arrowStartButton);
        _lineOptions.Children.Add(_arrowEndButton);
        _lineOptions.Children.Add(OptLabel(Loc.T("lbl.color")));
        AddColorSwatches(_lineOptions);
        RefreshArrowToggles();
        host.Children.Add(_lineOptions);

        // ---- Blur / mosaic: strength (edits the active kind's value) ----
        _blurOptions = NewOptionsRow();
        _blurOptions.Children.Add(OptLabel(Loc.T("lbl.blurStrength")));
        _blurSlider = new Slider { Minimum = 2, Maximum = 40, Width = 130, VerticalAlignment = VerticalAlignment.Center, Value = _gaussStrength };
        _blurSlider.ValueChanged += (_, se) =>
        {
            if (_blurSliderSync) return;
            if (_tool == Tool.Mosaic) _mosaicStrength = se.NewValue;
            else _gaussStrength = se.NewValue;
        };
        _blurOptions.Children.Add(_blurSlider);
        _blurOptions.Children.Add(ValueReadout(_blurSlider, v => v.ToString("0")));
        host.Children.Add(_blurOptions);

        // ---- Sticker: add image + hint ----
        _stickerOptions = NewOptionsRow();
        var addImage = new Button
        {
            Content = Loc.T("sticker.choose"),
            Padding = new Thickness(10, 3, 10, 3),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            FontSize = 12,
            Cursor = Cursors.Hand,
        };
        addImage.Click += (_, _) => AddSticker();
        _stickerOptions.Children.Add(addImage);
        _stickerOptions.Children.Add(new TextBlock
        {
            Text = Loc.T("sticker.hint"),
            Foreground = Theme.ForegroundBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        host.Children.Add(_stickerOptions);
    }

    private static StackPanel NewOptionsRow() => new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 6, 0, 0),
        Visibility = Visibility.Collapsed,
    };

    private static TextBlock OptLabel(string t) => new()
    {
        Text = t,
        Foreground = Theme.ForegroundBrush,
        FontSize = 12,
        Margin = new Thickness(8, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

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
        Cursor = Cursors.Hand,
    };

    private Button MakeStyleToggle(string label, Action onClick)
    {
        var b = MakeSmallToggle(label);
        b.Width = 24;
        b.Height = 24;
        b.Padding = new Thickness(0);
        b.FontWeight = label == "B" ? FontWeights.Bold : FontWeights.Normal;
        b.FontStyle = label == "I" ? FontStyles.Italic : FontStyles.Normal;
        if (label == "S") b.Content = new TextBlock { Text = "S", TextDecorations = TextDecorations.Strikethrough };
        b.Click += (_, _) => onClick();
        return b;
    }

    private Button MakeShapeKindButton(string text, ShapeKind kind)
    {
        var b = MakeSmallToggle(text);
        b.Tag = kind;
        b.Click += (_, _) => { _shapeKind = kind; RefreshShapeKindButtons(); };
        _shapeKindButtons.Add(b);
        return b;
    }

    private Button MakeShapeStyleButton(string text, bool filled)
    {
        var b = MakeSmallToggle(text);
        b.Tag = filled;
        b.Click += (_, _) => { _shapeFilled = filled; RefreshShapeStyleButtons(); };
        _shapeStyleButtons.Add(b);
        return b;
    }

    private void RefreshShapeKindButtons()
    {
        foreach (var b in _shapeKindButtons)
            b.Background = (ShapeKind)b.Tag! == _shapeKind ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private void RefreshShapeStyleButtons()
    {
        foreach (var b in _shapeStyleButtons)
            b.Background = (bool)b.Tag! == _shapeFilled ? Theme.ActiveBrush : Theme.ButtonBgBrush;
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
        }), DispatcherPriority.Input);
    }

    // ---------------------------------------------------------- colour swatches ---

    private void AddColorSwatches(StackPanel panel)
    {
        foreach (var c in Palette) panel.Children.Add(MakeSwatch(c));
        foreach (var c in _customColors) panel.Children.Add(MakeCustomSwatch(c));
        panel.Children.Add(MakeMoreColorsButton());
    }

    private Button MakeSwatch(Color c)
    {
        var b = new Button
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(2, 0, 2, 0),
            Background = Theme.SwatchBrush(c),
            BorderBrush = SwatchBorder,
            BorderThickness = new Thickness(1),
            Tag = c,
        };
        b.Click += (_, _) => OnColorPicked(c);
        return b;
    }

    private Button MakeCustomSwatch(Color c)
    {
        var b = MakeSwatch(c);
        b.ToolTip = Loc.T("color.removeHint");
        b.MouseRightButtonUp += (_, e) => { RemoveCustomColor(c); e.Handled = true; };
        _customSwatchButtons.Add(b);
        return b;
    }

    private void AddCustomColor(Color c)
    {
        if (Palette.Contains(c) || _customColors.Contains(c)) return;
        _customColors.Add(c);
        if (_customColors.Count > MaxCustomColors) _customColors.RemoveAt(0);
        foreach (var panel in new[] { _textOptions, _shapeOptions, _lineOptions })
            panel.Children.Insert(panel.Children.Count - 1, MakeCustomSwatch(c));   // before the "+"
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

    private Color ToolColor(Tool t) => t switch
    {
        Tool.Text => _textColor,
        Tool.Line => _lineColor,
        _ => _shapeColor,
    };

    private void RefreshSwatchSelection()
    {
        var cur = ToolColor(_tool);
        foreach (var panel in new[] { _textOptions, _shapeOptions, _lineOptions })
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

    private void OnColorPicked(Color c)
    {
        if (_tool == Tool.Text) { _textColor = Color.FromArgb(_textColor.A, c.R, c.G, c.B); ApplyTextStyle(); RefocusText(); }
        else if (_tool == Tool.Line) { _lineColor = Color.FromArgb(_lineColor.A, c.R, c.G, c.B); }
        else { _shapeColor = Color.FromArgb(_shapeColor.A, c.R, c.G, c.B); }
        RefreshSwatchSelection();
    }

    private void OpenColorPicker()
    {
        var current = ToolColor(_tool);
        double opacity = current.A / 255.0;

        // Anchor the picker to this window's rectangle (physical px).
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.GetWindowRect(hwnd, out var wr);
        var anchor = new Rect(wr.Left, wr.Top, wr.Width, wr.Height);

        var dlg = new ColorPickerWindow(current, opacity, anchor) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var a = (byte)Math.Round(dlg.SelectedOpacity * 255);
        var picked = Color.FromArgb(a, dlg.SelectedColor.R, dlg.SelectedColor.G, dlg.SelectedColor.B);
        if (_tool == Tool.Text) { _textColor = picked; ApplyTextStyle(); RefocusText(); }
        else if (_tool == Tool.Line) { _lineColor = picked; }
        else { _shapeColor = picked; }

        AddCustomColor(dlg.SelectedColor);
        RefreshSwatchSelection();
    }

    // ------------------------------------------------------------- keyboard ---

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_exporting)
        {
            if (e.Key == Key.Escape) { _exportCts?.Cancel(); e.Handled = true; }
            return;
        }

        // While typing an annotation, the box's own PreviewKeyDown handles commit keys.
        if (_editingText != null) return;

        if (Matches(_undoGesture, e)) { _history.Undo(); e.Handled = true; return; }
        if (Matches(_redoGesture, e)) { _history.Redo(); e.Handled = true; return; }
        // Copy = quick-export to the save folder and put the FILE on the clipboard
        // (an animated GIF can't ride the clipboard as an image), then close.
        if (Matches(_copyGesture, e)) { Export(quick: true, copyToClipboard: true); e.Handled = true; return; }

        if (e.Key == Key.Delete && _selected != null)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }

        double frame = 1.0 / _fps;
        double pos = _player.Position.TotalSeconds;
        switch (e.Key)
        {
            case Key.Space: TogglePlay(); e.Handled = true; break;
            case Key.Escape:
                // Esc walks outward: deselect annotation → drop the tool → close.
                if (_selected != null) Deselect();
                else if (_tool != Tool.None) SelectTool(_tool);
                else Close();
                e.Handled = true;
                break;
            case Key.Left: SetPlaying(false); Seek(pos - frame); e.Handled = true; break;
            case Key.Right: SetPlaying(false); Seek(pos + frame); e.Handled = true; break;
            case Key.Home: SetPlaying(false); Seek(_trimStart); e.Handled = true; break;
            case Key.End: SetPlaying(false); Seek(_trimEnd); e.Handled = true; break;
            case Key.I:
                _trimStart = Math.Clamp(pos, 0, Math.Max(0, _trimEnd - MinTrimGap));
                LayoutTimeline(); e.Handled = true; break;
            case Key.O:
                _trimEnd = Math.Clamp(pos, Math.Min(_duration, _trimStart + MinTrimGap), _duration);
                LayoutTimeline(); e.Handled = true; break;
        }
    }

    private bool Matches(KeyGesture? g, KeyEventArgs e) => g != null && g.Matches(this, e);

    // --------------------------------------------------------------- export ---

    private async void Export(bool quick, bool copyToClipboard = false)
    {
        if (_exporting || _duration <= 0) return;
        CommitActiveText();

        var format = RecordingFormats.Parse((string)((ComboBoxItem)_format.SelectedItem).Tag!);
        string? path = quick ? QuickPath(format) : AskPath(format);
        if (path == null) return;

        var ffmpeg = FFmpegLocator.Find();
        if (ffmpeg == null)
        {
            MessageBox.Show(this, Loc.T("rec.noFfmpeg"), "ScreenPaste",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        SetPlaying(false);
        _exporting = true;
        _format.IsEnabled = false;
        _toolsPanel.IsEnabled = false;
        _interact.IsHitTestVisible = false;
        _actions.Visibility = Visibility.Collapsed;
        _progressRow.Visibility = Visibility.Visible;
        _progressBar.Value = 0;
        _progressText.Text = Loc.T("edit.exporting", 0);

        var exporter = new RecordingExporter();
        exporter.Progress += p => Dispatcher.BeginInvoke(() =>
        {
            _progressBar.Value = p * 100;
            _progressText.Text = Loc.T("edit.exporting", (int)(p * 100));
        });

        string? overlayPng = null;
        bool ok, cancelled;
        try
        {
            overlayPng = RenderAnnotationOverlay();
            _exportCts = new CancellationTokenSource();
            ok = await exporter.ExportAsync(ffmpeg, _sourcePath, path, format,
                _trimStart, _trimEnd - _trimStart, _fps,
                overlayPng, _blurs.Select(b => b.Region).ToList(), _exportCts.Token);
            cancelled = _exportCts.IsCancellationRequested;
            _exportCts.Dispose();
            _exportCts = null;
        }
        finally
        {
            if (overlayPng != null)
                try { File.Delete(overlayPng); } catch { /* temp cleanup */ }
        }

        if (ok)
        {
            _saved = true;
            _settings.RecordFormat = format.Token();
            PersistToolSettings();
            if (copyToClipboard)
            {
                try
                {
                    var files = new System.Collections.Specialized.StringCollection { path };
                    Clipboard.SetFileDropList(files);
                }
                catch { /* clipboard busy — the file is saved regardless */ }
            }
            _onSaved?.Invoke(path);
            Close();
            return;
        }

        _exporting = false;
        _format.IsEnabled = true;
        _toolsPanel.IsEnabled = true;
        _interact.IsHitTestVisible = ToolNeedsInteract(_tool);
        _actions.Visibility = Visibility.Visible;
        _progressRow.Visibility = Visibility.Collapsed;
        if (!cancelled)
            MessageBox.Show(this, Loc.T("rec.failed", "ffmpeg"), "ScreenPaste",
                MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Rasterize the annotation layer (text/shapes/stickers) to a transparent PNG at the
    /// video's pixel size, for ffmpeg to composite. Returns null when there is nothing.
    /// </summary>
    private string? RenderAnnotationOverlay()
    {
        if (_annoHost.Children.Count == 0) return null;

        var rtb = new RenderTargetBitmap(_videoW, _videoH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(_annoHost);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        var dir = Path.Combine(Path.GetTempPath(), "ScreenPaste");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ov_" + Guid.NewGuid().ToString("N") + ".png");
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    private string QuickPath(RecordingFormat format)
    {
        Directory.CreateDirectory(_settings.SaveDirectory);
        return Path.Combine(_settings.SaveDirectory,
            "ScreenPaste_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + format.Extension());
    }

    private string? AskPath(RecordingFormat format)
    {
        Directory.CreateDirectory(_settings.SaveDirectory);
        string ext = format.Extension();
        var dlg = new SaveFileDialog
        {
            InitialDirectory = _settings.SaveDirectory,
            FileName = "ScreenPaste_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            Filter = $"{format.Token().ToUpperInvariant()} (*{ext})|*{ext}",
            DefaultExt = ext,
            AddExtension = true,
        };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    /// <summary>Shared tool defaults flow back to settings, same as the capture editor.</summary>
    private void PersistToolSettings()
    {
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
        _settings.GaussianStrength = _gaussStrength;
        _settings.MosaicStrength = _mosaicStrength;
        _settings.CustomColors = _customColors.Select(ToHex).ToList();
        _settings.Save();
    }

    // -------------------------------------------------------------- closing ---

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_exporting) { _exportCts?.Cancel(); base.OnClosing(e); return; }

        if (!_saved && _duration > 0 && _settings.ConfirmDiscardEdits)
        {
            var dlg = new ConfirmDiscardDialog(Loc.T("edit.title"), Loc.T("edit.discard")) { Owner = this };
            bool? discard = dlg.ShowDialog();
            if (dlg.DontAskAgain)
            {
                _settings.ConfirmDiscardEdits = false;
                _settings.Save();
            }
            if (discard != true) { e.Cancel = true; return; }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _ticker.Stop();
        _player.Close();   // release the file handle before deleting
        try { File.Delete(_sourcePath); } catch { /* temp cleanup is best-effort */ }
        base.OnClosed(e);
    }

    // -------------------------------------------------------------- widgets ---

    private Button MakeToolButton(string glyph, string tooltip, Tool tool)
    {
        var b = MakeIconButton(glyph, tooltip, () => SelectTool(tool));
        b.Tag = tool;
        _toolButtons.Add(b);
        return b;
    }

    private Button MakeToolTextButton(string text, Tool tool)
    {
        var b = new Button
        {
            Content = text,
            Tag = tool,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => SelectTool(tool);
        _toolButtons.Add(b);
        return b;
    }

    /// <summary>Icon-only button rendered with the Segoe MDL2 Assets glyph font.</summary>
    private static Button MakeIconButton(string glyph, string tooltip, Action onClick)
    {
        var b = new Button
        {
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 30,
            Height = 26,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(0),
            Foreground = Theme.ForegroundBrush,
            Background = Theme.ButtonBgBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button ActionButton(string text, Action onClick, bool isDefault = false)
    {
        var b = new Button
        {
            Content = text,
            MinWidth = 84,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            IsDefault = isDefault,
            Background = Theme.ButtonBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ButtonBorderBrush,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
