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
/// Drag to move, mouse-wheel to zoom at the cursor (Ctrl: opacity), middle-click to reset to
/// 100% and back, right-click or the Menu key for actions, Esc to close; the other keys are
/// PinWindow.Keyboard.cs's.
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
    private static readonly SolidColorBrush FrameBrush = CreateFrameBrush();

    private readonly AppSettings _settings;
    private readonly int _startX, _startY;     // physical px: where the image goes when first shown
    private readonly Image _image;
    private readonly Rectangle _saveFlash;     // quick save's white flash over the image
    private readonly Border _frame;
    private readonly MatrixTransform _layout = new();   // orientation and zoom, image px to DIPs
    private BitmapSource _source;              // the pinned image as it came
    private BitmapSource? _effected;           // _source with the effects in _effectedFor
    private (bool Grayscale, bool Invert) _effectedFor;
    private PinTransformState _state = PinTransformState.Identity;
    private double _dpi = 1.0;
    private bool _placed;                      // the window has been put on screen
    private bool _closed;

    public PinWindow(BitmapSource image, int screenX, int screenY, AppSettings settings)
    {
        _source = image;
        _startX = screenX;
        _startY = screenY;
        _settings = settings;

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

        // The image and its flash turn and zoom together; the frame stays one pixel wide.
        var content = new Grid { LayoutTransform = _layout };
        content.Children.Add(_image);
        content.Children.Add(_saveFlash);
        _frame = new Border
        {
            Child = content,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(FramePx),
        };
        Content = _frame;

        BuildContextMenu();
        InitKeyboard();

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        MouseDown += OnMiddleDown;
        MouseWheel += OnWheel;
        SourceInitialized += OnSourceInitialized;
        DpiChanged += OnDpiChanged;
        Closed += (_, _) => _closed = true;

        PinManager.Add(this);
    }

    private static SolidColorBrush CreateFrameBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0xAA, 0x3D, 0xA9, 0xFC));
        brush.Freeze();
        return brush;
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

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
    /// <paramref name="zoomAnchor"/> (physical screen px) still; a quarter turn keeps the centre.</summary>
    private void Apply(PinTransformState next, Point zoomAnchor)
    {
        if (next == _state) return;
        var (w, h) = OutputSize(next);
        bool quarterTurn = next.QuarterTurned != _state.QuarterTurned;
        Present(next, PinGeometry.NextTopLeft(ImageRect(), zoomAnchor, _state.Zoom, next.Zoom, w, h, quarterTurn));
    }

    /// <summary>A change from a key or the menu: a zoom stays centred.</summary>
    private void Apply(PinTransformState next) => Apply(next, Centre(ImageRect()));

    /// <summary>Show <paramref name="next"/> with the image's top-left at
    /// <paramref name="imageTopLeft"/> (physical screen px).</summary>
    private void Present(PinTransformState next, Point imageTopLeft)
    {
        _state = next;
        UpdateLayoutTransform();
        _image.Source = Effected(next);
        Opacity = next.Opacity;

        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        var (w, h) = OutputSize(next);
        // The first placement also shows the window, which Show() is still on its way to doing.
        uint flags = NativeMethods.SWP_NOACTIVATE | (_placed ? 0u : NativeMethods.SWP_SHOWWINDOW);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            (int)Math.Round(imageTopLeft.X) - FramePx, (int)Math.Round(imageTopLeft.Y) - FramePx,
            w + 2 * FramePx, h + 2 * FramePx, flags);
        _placed = true;
    }

    /// <summary>Turn, flip and scale the image so it fills its rounded output size exactly,
    /// in DIPs for the current monitor; the frame stays one physical pixel.</summary>
    private void UpdateLayoutTransform()
    {
        var (sx, sy) = PinGeometry.PixelScale(_source.PixelWidth, _source.PixelHeight, _state.Rotation, _state.Zoom);
        _layout.Matrix = _state.Orientation * new Matrix(sx / _dpi, 0, 0, sy / _dpi, 0, 0);
        _frame.BorderThickness = new Thickness(FramePx / _dpi);
    }

    /// <summary>Where the image is now (physical screen px): read back from the window, which
    /// may have been dragged, or where it goes before the window is first shown.</summary>
    private Rect ImageRect()
    {
        var (w, h) = OutputSize(_state);
        if (_placed && NativeMethods.GetWindowRect(Handle, out var r))
            return new Rect(r.Left + FramePx, r.Top + FramePx, w, h);
        return new Rect(_startX, _startY, w, h);
    }

    private (int Width, int Height) OutputSize(PinTransformState state) =>
        PinGeometry.OutputPixelSize(_source.PixelWidth, _source.PixelHeight, state.Rotation, state.Zoom);

    private static Point Centre(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>The image with the state's effects, kept until they change.</summary>
    private BitmapSource Effected(PinTransformState state)
    {
        if (!state.Grayscale && !state.Invert) return _source;
        if (_effected == null || _effectedFor != (state.Grayscale, state.Invert))
        {
            _effected = PinRenderer.Effects(_source, state);
            _effectedFor = (state.Grayscale, state.Invert);
        }
        return _effected;
    }

    /// <summary>What copy, save and print take: the image as the pin shows it, at its own resolution.</summary>
    private BitmapSource RenderedImage() => PinRenderer.Orient(Effected(_state), _state);

    // ------------------------------------------------------------- mouse ---

    /// <summary>The wheel zooms, anchored at the cursor (or the top-left with PinZoomAtCursor
    /// off); with Ctrl it steps the opacity instead.</summary>
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        int notch = Math.Sign(e.Delta);
        if (notch == 0) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Apply(_state.WithOpacityStep(notch));
            return;
        }
        Apply(_state.ZoomBy(notch > 0 ? ZoomStep : 1 / ZoomStep), PointerAnchor());
    }

    /// <summary>A middle-click resets to 100% size and opacity; the next one goes back.</summary>
    private void OnMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        e.Handled = true;
        Apply(_state.ToggleReset(), PointerAnchor());
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
        menu.Items.Clear();
        menu.Items.Add(MenuEntry("pin.copy", Gesture(_settings.CopyHotkey), PinCommand.Copy));
        menu.Items.Add(FixedEntry("pin.copyFile", PinCommand.CopyAsFile));
        menu.Items.Add(MenuEntry("pin.save", Gesture(_settings.SaveHotkey), PinCommand.Save));
        menu.Items.Add(MenuEntry("pin.quickSave", Gesture(_settings.QuickSaveHotkey), PinCommand.QuickSave));
        menu.Items.Add(MenuEntry("pin.print", Gesture(_settings.PrintHotkey), PinCommand.Print));
        menu.Items.Add(new Separator());
        menu.Items.Add(FixedEntry("pin.rotateCw", PinCommand.RotateClockwise));
        menu.Items.Add(FixedEntry("pin.rotateCcw", PinCommand.RotateCounterClockwise));
        menu.Items.Add(FixedEntry("pin.flipH", PinCommand.FlipHorizontal));
        menu.Items.Add(FixedEntry("pin.flipV", PinCommand.FlipVertical));
        menu.Items.Add(FixedEntry("pin.grayscale", PinCommand.ToggleGrayscale, isChecked: _state.Grayscale));
        menu.Items.Add(FixedEntry("pin.invert", PinCommand.ToggleInvert, isChecked: _state.Invert));
        menu.Items.Add(new Separator());
        menu.Items.Add(FixedEntry("pin.opacityUp", PinCommand.OpacityUp,
            isEnabled: _state.Opacity < PinTransformState.MaxOpacity));
        menu.Items.Add(FixedEntry("pin.opacityDown", PinCommand.OpacityDown,
            isEnabled: _state.Opacity > PinTransformState.MinOpacity));
        menu.Items.Add(MenuEntry(Loc.T("pin.reset"), Loc.T("set.middleClickAction"),
            () => Apply(_state.Reset()), isChecked: false, isEnabled: !_state.IsAtRest));
        menu.Items.Add(new Separator());
        menu.Items.Add(FixedEntry("tray.settings", PinCommand.OpenSettings));
        menu.Items.Add(FixedEntry("pin.close", PinCommand.Close));
    }

    /// <summary>A configurable gesture as the settings window writes it; null when unbound.</summary>
    private static string? Gesture(string? hotkey) => KeyChord.ParseGesture(hotkey)?.ToString();

    /// <summary>A row for a command on a fixed key, labelled from the key table itself.</summary>
    private MenuItem FixedEntry(string labelKey, PinCommand command, bool isChecked = false, bool isEnabled = true) =>
        MenuEntry(labelKey, PinKeyRouter.FixedKeys(command), command, isChecked, isEnabled);

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
