using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenPaste.Input;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>
/// A frameless, always-on-top window that pins a composed image onto the screen.
/// Mouse-wheel to zoom at the cursor (Ctrl: opacity), middle-click to reset to 100% and back,
/// right-click or the Menu key for actions; the keys are PinWindow.Keyboard.cs's (Ctrl+V shows
/// the clipboard instead, Ctrl+Shift+C copies a text pin's text), closing, destroying, a
/// double-click and dropped files PinWindow.Lifecycle.cs's, dragging, Shift-snapping and
/// border resizing PinWindow.Drag.cs's, thumbnail mode PinWindow.Thumbnail.cs's and the Alt
/// loupe PinWindow.Loupe.cs's. The click-through hotkey can make the pin let clicks through to
/// the window beneath (<see cref="SetClickThrough"/>). Pins picked together (Ctrl+click,
/// Ctrl+A) move, fade, reset, close and destroy as one (<see cref="PinManager.Selection"/>).
///
/// The pin shows its image through a <see cref="PinTransformState"/> (zoom, quarter turns,
/// flips, opacity, grayscale, invert). Content is authored in physical px and the window is
/// sized and placed in physical px via SetWindowPos, so it stays pixel-aligned. Where the
/// pin is gets read back from the window before every change, since it may have been
/// dragged or moved since; copy, save and print take the image as it is shown.
/// </summary>
public sealed partial class PinWindow : Window
{
    private const int FramePx = 1;             // the frame around the image, outside it
    private const double ZoomStep = 1.1;       // one wheel notch, or one + / - press
    private const double SaveFlashOpacity = 0.6;
    private const double MenuCheckWidth = 20;
    private const double MenuKeysGap = 28;
    private const double MenuKeysOpacity = 0.6;
    private const string MenuCheckMark = "✓";
    private static readonly Duration SaveFlashDuration = new(TimeSpan.FromMilliseconds(400));
    private static readonly SolidColorBrush FrameBrush = CreateFrameBrush(Color.FromArgb(0xAA, 0x3D, 0xA9, 0xFC));
    private static readonly SolidColorBrush ClickThroughFrameBrush = CreateFrameBrush(Color.FromArgb(0x40, 0x3D, 0xA9, 0xFC));
    private static readonly SolidColorBrush SelectedFrameBrush = CreateFrameBrush(Color.FromRgb(0xFF, 0x9F, 0x1C));

    private readonly AppSettings _settings;
    private readonly int _startX, _startY;     // physical px: where the image goes when first shown
    private readonly Image _image;
    private readonly Rectangle _saveFlash;     // quick save's white flash over the image
    private readonly Grid _layers;             // the image and its flash
    private readonly Canvas _viewport;         // what the window shows: all of the layers, or a thumbnail's part
    private readonly Border _frame;
    private PinContent _content;               // what is pinned, as it came; a dropped file replaces it
    private BitmapSource? _effected;           // Source with the effects in _effectedFor
    private (bool Grayscale, bool Invert) _effectedFor;
    private PinTransformState _state;
    private double _dpi = 1.0;
    private bool _placed;                      // the window has been put on screen
    private bool _closed;
    private bool _clickThrough;                // clicks go to the window beneath

    public PinWindow(BitmapSource image, int screenX, int screenY, AppSettings settings)
        : this(new PinContent(image), screenX, screenY, settings, PinTransformState.Identity)
    {
    }

    /// <summary>A pin showing <paramref name="content"/> as <paramref name="state"/> turns,
    /// zooms and fades it, the image's top-left at (<paramref name="screenX"/>,
    /// <paramref name="screenY"/>) in physical screen px.</summary>
    public PinWindow(PinContent content, int screenX, int screenY, AppSettings settings, PinTransformState state)
    {
        _content = content;
        _state = state;
        _startX = screenX;
        _startY = screenY;
        _settings = settings;
        var image = content.Image;

        Title = "ScreenPaste Pin";   // not displayed (borderless) — used for identification
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SnapsToDevicePixels = true;

        _image = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = image.PixelWidth,
            Height = image.PixelHeight,
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _saveFlash = new Rectangle { Fill = Brushes.White, Opacity = 0, IsHitTestVisible = false };

        // The image and its flash turn and zoom together; the frame stays one pixel wide. The
        // viewport clips them to what the window shows, which a thumbnail makes a part of them.
        _layers = new Grid();
        _layers.Children.Add(_image);
        _layers.Children.Add(_saveFlash);
        _viewport = new Canvas { ClipToBounds = true };
        _viewport.Children.Add(_layers);
        _frame = new Border
        {
            Child = _viewport,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(FramePx),
        };
        Content = _frame;

        BuildContextMenu();
        InitKeyboard();
        InitLifecycle();
        InitDrag();
        InitThumbnail();
        InitLoupe();

        MouseDown += OnMiddleDown;
        MouseWheel += OnWheel;
        SourceInitialized += OnSourceInitialized;
        DpiChanged += OnDpiChanged;
        Closed += (_, _) => _closed = true;

        PinManager.Add(this);
    }

    private static SolidColorBrush CreateFrameBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>The pinned image as it came, before its turn, zoom and effects.</summary>
    private BitmapSource Source => _content.Image;

    /// <summary>The settings this pin follows (shared with the app, so changes apply at once).</summary>
    internal AppSettings Settings => _settings;

    // ------------------------------------------------------------ layout ---

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        uint dpi = NativeMethods.GetDpiForWindow(Handle);
        _dpi = dpi == 0 ? 1.0 : dpi / 96.0;
        Present(_state, new Point(_startX, _startY));
    }

    /// <summary>
    /// Moved onto a monitor with another scale: WPF rescales the content and resizes the
    /// window by the system's suggestion, but the pin is authored in physical px, so both go
    /// back once WPF is done. The first placement can land here too (the window is created on
    /// the default monitor); then the start position is the one to keep.
    /// </summary>
    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        _dpi = e.NewDpi.DpiScaleX;
        UpdateLayoutTransform();
        bool firstPlacement = !_placed;
        Dispatcher.InvokeAsync(() =>
        {
            if (_closed) return;
            Present(_state, firstPlacement ? new Point(_startX, _startY) : ImageRect().TopLeft);
        }, DispatcherPriority.Normal);
    }

    /// <summary>Change to <paramref name="next"/>. A new zoom keeps the image point at
    /// <paramref name="zoomAnchor"/> (physical screen px) still; a quarter turn keeps the centre.
    /// A thumbnail shows a part of the image as it is turned, at its own size, so a turn, a
    /// flip or a zoom brings the whole pin back first; opacity and effects keep it.</summary>
    private void Apply(PinTransformState next, Point zoomAnchor)
    {
        if (next == _state) return;
        if (_thumbnail != null && ReshapesPin(next)) ExitThumbnail();
        var (w, h) = OutputSize(next);
        bool quarterTurn = next.QuarterTurned != _state.QuarterTurned;
        Present(next, PinGeometry.NextTopLeft(ImageRect(), zoomAnchor, _state.Zoom, next.Zoom, w, h, quarterTurn));
    }

    /// <summary>A change from a key or the menu: a zoom stays centred.</summary>
    private void Apply(PinTransformState next) => Apply(next, Centre(ImageRect()));

    private bool ReshapesPin(PinTransformState next) =>
        next.Zoom != _state.Zoom || next.Rotation != _state.Rotation ||
        next.FlipHorizontal != _state.FlipHorizontal || next.FlipVertical != _state.FlipVertical;

    /// <summary>Show <paramref name="next"/> with the image's (or the thumbnail's) top-left at
    /// <paramref name="imageTopLeft"/> (physical screen px).</summary>
    private void Present(PinTransformState next, Point imageTopLeft)
    {
        _state = next;
        UpdateLayoutTransform();
        _image.Source = Effected(next);
        Opacity = next.Opacity;

        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        var (w, h) = ShownSize();
        // The first placement also shows the window, which Show() is still on its way to doing.
        uint flags = NativeMethods.SWP_NOACTIVATE | (_placed ? 0u : NativeMethods.SWP_SHOWWINDOW);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            (int)Math.Round(imageTopLeft.X) - FramePx, (int)Math.Round(imageTopLeft.Y) - FramePx,
            w + 2 * FramePx, h + 2 * FramePx, flags);
        _placed = true;
    }

    /// <summary>Turn, flip and scale the image so it fills its rounded output size exactly,
    /// in DIPs for the current monitor; the frame stays one physical pixel. The viewport is
    /// the size the window shows, and a thumbnail shifts the image so its part is in view.</summary>
    private void UpdateLayoutTransform()
    {
        var (sx, sy) = DisplayScale();
        // A new transform each time: WPF lays the layers out before SourceInitialized, and a
        // new matrix on the transform they already hold neither lays them out again nor
        // redraws them, so at 175% the image stayed 1.75 times too big, cropped by the window.
        var layout = new MatrixTransform(_state.Orientation * new Matrix(sx / _dpi, 0, 0, sy / _dpi, 0, 0));
        layout.Freeze();
        _layers.LayoutTransform = layout;
        _frame.BorderThickness = new Thickness(FramePx / _dpi);

        var (w, h) = ShownSize();
        _viewport.Width = w / _dpi;
        _viewport.Height = h / _dpi;
        var crop = _thumbnail?.Crop ?? new Rect();
        Canvas.SetLeft(_layers, -crop.X * sx / _dpi);
        Canvas.SetTop(_layers, -crop.Y * sy / _dpi);
    }

    /// <summary>Screen px per image px along each screen axis as the pin shows it now: at its
    /// zoom, or at a thumbnail's scale.</summary>
    private (double X, double Y) DisplayScale() =>
        PinGeometry.PixelScale(Source.PixelWidth, Source.PixelHeight, _state.Rotation, _thumbnail?.Scale ?? _state.Zoom);

    /// <summary>Where the image (or the thumbnail) is now (physical screen px): read back from
    /// the window, which may have been dragged, or where it goes before it is first shown.</summary>
    private Rect ImageRect()
    {
        var (w, h) = ShownSize();
        if (_placed && NativeMethods.GetWindowRect(Handle, out var r))
            return new Rect(r.Left + FramePx, r.Top + FramePx, w, h);
        return new Rect(_startX, _startY, w, h);
    }

    private (int Width, int Height) OutputSize(PinTransformState state) =>
        PinGeometry.OutputPixelSize(Source.PixelWidth, Source.PixelHeight, state.Rotation, state.Zoom);

    /// <summary>What the window shows (physical px, frame excluded): the image as turned and
    /// zoomed, or the thumbnail.</summary>
    private (int Width, int Height) ShownSize() => _thumbnail?.OutputSize ?? OutputSize(_state);

    /// <summary>The image's size as turned, in image px.</summary>
    private (int Width, int Height) TurnedSourceSize() => _state.QuarterTurned
        ? (Source.PixelHeight, Source.PixelWidth)
        : (Source.PixelWidth, Source.PixelHeight);

    private static Point Centre(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>The image with the state's effects, kept until they change.</summary>
    private BitmapSource Effected(PinTransformState state)
    {
        if (!state.Grayscale && !state.Invert) return Source;
        if (_effected == null || _effectedFor != (state.Grayscale, state.Invert))
        {
            _effected = PinRenderer.Effects(Source, state);
            _effectedFor = (state.Grayscale, state.Invert);
        }
        return _effected;
    }

    /// <summary>What copy, save and print take: the image as the pin shows it, at its own resolution.</summary>
    private BitmapSource RenderedImage() => PinRenderer.Orient(Effected(_state), _state);

    /// <summary>Where the window is on screen now (physical px), frame included; empty before it is.</summary>
    internal Rect ScreenRect() =>
        _placed && NativeMethods.GetWindowRect(Handle, out var r) ? new Rect(r.Left, r.Top, r.Width, r.Height) : Rect.Empty;

    // ------------------------------------------------------- click-through ---

    /// <summary>Whether clicks go through the pin to the window beneath (the click-through hotkey).</summary>
    public bool IsClickThrough => _clickThrough;

    /// <summary>Let clicks through to the window beneath, or take them again. The frame dims
    /// while they go through, as the pin can then be neither dragged nor right-clicked.</summary>
    public void SetClickThrough(bool on)
    {
        var hwnd = Handle;
        if (_closed || hwnd == IntPtr.Zero || on == _clickThrough) return;
        _clickThrough = on;
        // The window is already layered (AllowsTransparency); transparent makes it click-through.
        if (on) NativeMethods.AddExStyle(hwnd, NativeMethods.WS_EX_TRANSPARENT);
        else NativeMethods.RemoveExStyle(hwnd, NativeMethods.WS_EX_TRANSPARENT);
        RefreshFrame();
    }

    // ----------------------------------------------------------- selection ---

    /// <summary>The frame says how the pin stands: dimmed while clicks go through it,
    /// highlighted while it is picked with others (Ctrl+click, Ctrl+A).</summary>
    internal void RefreshFrame()
    {
        if (_closed) return;
        _frame.BorderBrush = _clickThrough ? ClickThroughFrameBrush
            : PinManager.IsSelected(this) ? SelectedFrameBrush
            : FrameBrush;
    }

    /// <summary>Run <paramref name="run"/> on this pin, or, when it is picked, on every shown
    /// pin picked with it: moves, opacity, resets, closing and destroying act on them all.</summary>
    private void ForGroup(Action<PinWindow> run)
    {
        foreach (var pin in PinManager.GroupOf(this))
            if (!pin._closed) run(pin);
    }

    // ------------------------------------------------------------- mouse ---

    /// <summary>The wheel zooms, anchored at the cursor (or the top-left with PinZoomAtCursor
    /// off); with Ctrl it steps the opacity instead, of every pin picked with this one too.</summary>
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        int notch = Math.Sign(e.Delta);
        if (notch == 0) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ForGroup(pin => pin.Apply(pin._state.WithOpacityStep(notch)));
            return;
        }
        if (_resize != null) return;   // the border drag owns the size
        Apply(_state.ZoomBy(notch > 0 ? ZoomStep : 1 / ZoomStep), PointerAnchor());
    }

    /// <summary>A middle-click resets to 100% size and opacity; the next one goes back. Pins
    /// picked with this one reset about their own centres.</summary>
    private void OnMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        e.Handled = true;
        if (_move != null || _resize != null) return;
        var anchor = PointerAnchor();
        ForGroup(pin =>
        {
            if (pin == this) Apply(_state.ToggleReset(), anchor);
            else pin.Apply(pin._state.ToggleReset());
        });
    }

    /// <summary>Where a mouse zoom is anchored: the cursor, or the image's top-left (how pins
    /// always zoomed) with PinZoomAtCursor off.</summary>
    private Point PointerAnchor()
    {
        if (_settings.PinZoomAtCursor && NativeMethods.GetCursorPos(out var pt)) return new Point(pt.X, pt.Y);
        return ImageRect().TopLeft;
    }

    // ----------------------------------------------------------- outputs ---

    // Copy carries on when another app keeps the clipboard locked, as the capture's does.
    private void CopyImage() => ClipboardService.CopyImage(RenderedImage());

    private void CopyAsFile()
    {
        if (!ClipboardService.CopyAsFile(RenderedImage())) Warn(Loc.T("copyFile.failed"));
    }

    private void SaveAs()
    {
        var image = RenderedImage();
        string? failure = null;
        PinManager.ShowingDialog(() =>
        {
            try { FileSaveService.SaveAs(image, _settings.SaveDirectory); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failure = ex.Message; }
        });
        if (failure != null) Warn(Loc.T("pin.saveFailed", failure));
    }

    /// <summary>Straight into the default folder, with a short white flash to show it happened.</summary>
    private void QuickSave()
    {
        try
        {
            FileSaveService.QuickSave(RenderedImage(), _settings.SaveDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warn(Loc.T("pin.saveFailed", ex.Message));
            return;
        }
        _saveFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(SaveFlashOpacity, 0, SaveFlashDuration));
    }

    /// <summary>
    /// Print on one page. Once a printer is chosen the pin steps aside, so a printer's own
    /// dialog (a PDF printer asking for a file name) cannot open behind this topmost window,
    /// and it comes back afterwards however the job went.
    /// </summary>
    private void Print()
    {
        var image = RenderedImage();
        string? failure = null;
        PinManager.ShowingDialog(() =>
        {
            try
            {
                PrintService.Print(image, Loc.T("app.name"), beforePrinting: Hide);
            }
            // Printer drivers fail in many ways (spooler, XPS, Win32, COM).
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failure = ex.Message;
            }
            finally
            {
                if (!_closed && !IsVisible) Show();
            }
        });
        if (failure != null) Warn(Loc.T("print.failed", failure));
    }

    private void Warn(string message) => PinManager.ShowingDialog(() =>
        MessageBox.Show(this, message, "ScreenPaste", MessageBoxButton.OK, MessageBoxImage.Warning));

    // -------------------------------------------------------------- menu ---

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        Grid.SetIsSharedSizeScope(menu, true);   // the labels and keys line up in columns
        ContextMenu = menu;
        // Filled on every opening: the gestures, the language and the toggles may have changed.
        // Already open (the Menu key opened it): leave it as it is rather than refill it.
        ContextMenuOpening += (_, e) =>
        {
            if (menu.IsOpen) e.Handled = true;
            else FillMenu(menu);
        };
        // The Menu key places it itself; a right-click then goes back to the pointer.
        menu.Closed += (_, _) => menu.ClearValue(ContextMenu.PlacementProperty);
    }

    /// <summary>The Menu key: the right-click menu, at the pointer when it is over the pin,
    /// else over the pin's middle.</summary>
    private void OpenMenu()
    {
        if (ContextMenu is not { } menu || menu.IsOpen) return;
        FillMenu(menu);
        menu.PlacementTarget = this;
        menu.Placement = IsMouseOver ? PlacementMode.MousePoint : PlacementMode.Center;
        menu.IsOpen = true;
    }

    private void FillMenu(ContextMenu menu)
    {
        // Read per opening, so a key changed in the settings shows on open pins at once.
        var keys = ShortcutMap.FromSettings(_settings);
        MenuItem Entry(string labelKey, string id, PinCommand command, bool isChecked = false, bool isEnabled = true) =>
            MenuEntry(labelKey, keys.Label(id), command, isChecked, isEnabled);

        menu.Items.Clear();
        menu.Items.Add(Entry("pin.copy", PinShortcuts.Copy, PinCommand.Copy));
        menu.Items.Add(Entry("pin.copyFile", PinShortcuts.CopyAsFile, PinCommand.CopyAsFile));
        if (_content.SourceText != null) menu.Items.Add(Entry("pin.copyText", PinShortcuts.CopyText, PinCommand.CopyText));
        menu.Items.Add(Entry("pin.save", PinShortcuts.Save, PinCommand.Save));
        menu.Items.Add(Entry("pin.quickSave", PinShortcuts.QuickSave, PinCommand.QuickSave));
        menu.Items.Add(Entry("pin.print", PinShortcuts.Print, PinCommand.Print));
        menu.Items.Add(Entry("pin.paste", PinShortcuts.Paste, PinCommand.PasteClipboard));
        menu.Items.Add(new Separator());
        menu.Items.Add(Entry("pin.rotateCw", PinShortcuts.RotateClockwise, PinCommand.RotateClockwise));
        menu.Items.Add(Entry("pin.rotateCcw", PinShortcuts.RotateCounterClockwise, PinCommand.RotateCounterClockwise));
        menu.Items.Add(Entry("pin.flipH", PinShortcuts.FlipHorizontal, PinCommand.FlipHorizontal));
        menu.Items.Add(Entry("pin.flipV", PinShortcuts.FlipVertical, PinCommand.FlipVertical));
        menu.Items.Add(Entry("pin.grayscale", PinShortcuts.Grayscale, PinCommand.ToggleGrayscale, isChecked: _state.Grayscale));
        menu.Items.Add(Entry("pin.invert", PinShortcuts.Invert, PinCommand.ToggleInvert, isChecked: _state.Invert));
        menu.Items.Add(new Separator());
        menu.Items.Add(Entry("pin.opacityUp", PinShortcuts.OpacityUp, PinCommand.OpacityUp,
            isEnabled: _state.Opacity < PinTransformState.MaxOpacity));
        menu.Items.Add(Entry("pin.opacityDown", PinShortcuts.OpacityDown, PinCommand.OpacityDown,
            isEnabled: _state.Opacity > PinTransformState.MinOpacity));
        menu.Items.Add(MenuEntry(Loc.T("pin.reset"), Loc.T("set.middleClickAction"),
            () => ForGroup(pin => pin.Apply(pin._state.Reset())), isChecked: false, isEnabled: !_state.IsAtRest));
        menu.Items.Add(MenuEntry(Loc.T("pin.thumbnail"), "Shift+" + Loc.T("set.dblClickAction"),
            ToggleThumbnail, isChecked: _thumbnail != null, isEnabled: true));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuEntry(Loc.T("pin.dblClickClose"), null, ToggleDoubleClickClose,
            isChecked: DoubleClickCloses, isEnabled: true));
        menu.Items.Add(Entry("tray.settings", PinShortcuts.Settings, PinCommand.OpenSettings));
        menu.Items.Add(Entry("pin.close", PinShortcuts.Close, PinCommand.Close));
        menu.Items.Add(Entry("pin.destroy", PinShortcuts.Destroy, PinCommand.Destroy));
    }

    private MenuItem MenuEntry(string labelKey, string? keys, PinCommand command,
        bool isChecked = false, bool isEnabled = true) =>
        MenuEntry(Loc.T(labelKey), keys, () => RunCommand(command, default), isChecked, isEnabled);

    /// <summary>A menu row: a check mark column, the label, and the keys that do the same.</summary>
    private static MenuItem MenuEntry(string label, string? keys, Action run, bool isChecked, bool isEnabled)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MenuCheckWidth) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Keys" });
        AddMenuCell(header, 0, isChecked ? MenuCheckMark : "", new Thickness(0), 1.0);
        AddMenuCell(header, 1, label, new Thickness(0), 1.0);
        if (!string.IsNullOrEmpty(keys)) AddMenuCell(header, 2, keys, new Thickness(MenuKeysGap, 0, 0, 0), MenuKeysOpacity);

        var item = new MenuItem { Header = header, IsEnabled = isEnabled };
        item.Click += (_, _) => run();
        return item;
    }

    private static void AddMenuCell(Grid row, int column, string text, Thickness margin, double opacity)
    {
        var cell = new TextBlock { Text = text, Margin = margin, Opacity = opacity, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(cell, column);
        row.Children.Add(cell);
    }
}
