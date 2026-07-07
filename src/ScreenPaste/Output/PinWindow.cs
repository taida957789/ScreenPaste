using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Native;

namespace ScreenPaste.Output;

/// <summary>
/// A frameless, always-on-top window that pins a composed image onto the screen.
/// Drag to move, mouse-wheel to zoom, right-click for actions, Esc to close.
/// </summary>
public sealed class PinWindow : Window
{
    private readonly BitmapSource _image;
    private readonly int _screenX, _screenY;   // physical px anchor (top-left of original selection)
    private readonly ScaleTransform _scale = new(1, 1);
    private double _dpi = 1.0;
    private double _zoom = 1.0;

    public PinWindow(BitmapSource image, int screenX, int screenY, string saveDirectory)
    {
        _image = image;
        _screenX = screenX;
        _screenY = screenY;

        Title = "ScreenPaste Pin";   // not displayed (borderless) — used for identification
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SnapsToDevicePixels = true;

        var img = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = image.PixelWidth,
            Height = image.PixelHeight,
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

        var border = new Border
        {
            Child = img,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xAA, 0x3D, 0xA9, 0xFC)),
            BorderThickness = new Thickness(1),
            LayoutTransform = _scale,
        };
        Content = border;

        BuildContextMenu(saveDirectory);

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        MouseWheel += OnWheel;
        // A focused pin closes only itself; the global "close all when none focused"
        // case is handled by PinManager's low-level keyboard hook.
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        SourceInitialized += OnSourceInitialized;

        PinManager.Add(this);
    }

    private void BuildContextMenu(string saveDirectory)
    {
        var menu = new ContextMenu();

        var copy = new MenuItem { Header = "複製 (Ctrl+C)" };
        copy.Click += (_, _) => ClipboardService.CopyImage(_image);

        var save = new MenuItem { Header = "儲存…" };
        save.Click += (_, _) => FileSaveService.SaveAs(_image, saveDirectory);

        var reset = new MenuItem { Header = "重設縮放 (100%)" };
        reset.Click += (_, _) => { _zoom = 1.0; ApplyZoom(); };

        var close = new MenuItem { Header = "關閉 (Esc)" };
        close.Click += (_, _) => Close();

        menu.Items.Add(copy);
        menu.Items.Add(save);
        menu.Items.Add(new Separator());
        menu.Items.Add(reset);
        menu.Items.Add(close);
        ContextMenu = menu;

        // Ctrl+C shortcut while focused.
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => ClipboardService.CopyImage(_image)),
            Key.C, ModifierKeys.Control));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        _dpi = dpi <= 0 ? 1.0 : dpi / 96.0;
        ApplyZoom();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        // Content is authored in physical px; scale by zoom/dpi to render, and size the
        // window in physical px via SetWindowPos so it stays pixel-aligned.
        double s = _zoom / _dpi;
        _scale.ScaleX = s;
        _scale.ScaleY = s;

        int w = Math.Max(1, (int)Math.Round(_image.PixelWidth * _zoom));
        int h = Math.Max(1, (int)Math.Round(_image.PixelHeight * _zoom));

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                _screenX, _screenY, w, h,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
