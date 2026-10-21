using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScreenPaste.Native;
using ScreenPaste.Settings;
using Forms = System.Windows.Forms;

namespace ScreenPaste.Output;

/// <summary>
/// A pin's magnifier and colour picker (Snipaste's Alt on a pin), made to look like the
/// capture's: the pixels around the cursor enlarged with the one under it outlined, its
/// position in the image and its colour as written. A small topmost window that never takes
/// the focus or a click, so the pin keeps the keys (C, Shift) and the mouse. The pixels come
/// from the image as the pin shows it, not from the screen; <see cref="PinWindow"/> feeds it.
/// </summary>
internal sealed class PinLoupe : Window
{
    private const int SourceWidth = 30, SourceHeight = 22, Zoom = 6;   // 30x22 px shown at 180x132
    private const int CentreX = SourceWidth / 2, CentreY = SourceHeight / 2;
    private const double CrossWidth = Zoom;                            // one enlarged pixel wide
    private const double GapDip = 20;                                  // between the cursor and the loupe
    private static readonly SolidColorBrush CrossBrush = Frozen(Color.FromArgb(0x50, 0x3D, 0xA9, 0xFC));
    private static readonly SolidColorBrush PanelBrush = Frozen(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E));
    private static readonly SolidColorBrush HintBrush = Frozen(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush SwatchStroke = Frozen(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
    private static readonly FontFamily Mono = new("Consolas");

    private readonly ImageBrush _pixels;
    private readonly TextBlock _position;
    private readonly Rectangle _swatch;
    private readonly TextBlock _colour;

    public PinLoupe()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        // Outside the image the view stays black, so the cursor's pixel is always the centre one.
        _pixels = new ImageBrush { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill, TileMode = TileMode.None };
        RenderOptions.SetBitmapScalingMode(_pixels, BitmapScalingMode.NearestNeighbor);
        _position = Text(13, Brushes.White);
        _swatch = new Rectangle { Width = 13, Height = 13, Stroke = SwatchStroke, StrokeThickness = 1, VerticalAlignment = VerticalAlignment.Center };
        _colour = Text(13, Brushes.White);
        _colour.Margin = new Thickness(6, 0, 0, 0);

        var panel = new StackPanel();
        panel.Children.Add(View());
        panel.Children.Add(Readout());
        Content = panel;

        SourceInitialized += (_, _) => NativeMethods.AddExStyle(Handle,
            NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>Show pixel (<paramref name="x"/>, <paramref name="y"/>) of
    /// <paramref name="image"/> and its colour, beside <paramref name="cursor"/> (physical px).</summary>
    public void ShowPixel(BitmapSource image, int x, int y, Color colour, string colourText, Point cursor)
    {
        if (!ReferenceEquals(_pixels.ImageSource, image)) _pixels.ImageSource = image;
        // The brush counts in the image's DIPs, which a bitmap's own DPI can make differ from its pixels.
        double ux = image.Width / Math.Max(1, image.PixelWidth), uy = image.Height / Math.Max(1, image.PixelHeight);
        _pixels.Viewbox = new Rect((x - CentreX) * ux, (y - CentreY) * uy, SourceWidth * ux, SourceHeight * uy);
        _position.Text = FormattableString.Invariant($"({x}, {y})");
        _swatch.Fill = new SolidColorBrush(colour);
        _colour.Text = colourText;
        if (!IsVisible) Show();
        PlaceBeside(cursor);
    }

    /// <summary>Below and right of the cursor, flipped near the monitor's edges, and above
    /// the pins (one clicked since is raised over it).</summary>
    private void PlaceBeside(Point cursor)
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var bounds = Forms.Screen.FromPoint(new System.Drawing.Point((int)cursor.X, (int)cursor.Y)).Bounds;
        var monitor = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        double gap = GapDip * (dpi == 0 ? 1.0 : dpi / 96.0);
        var at = PinGeometry.LoupeAt(cursor, r.Width, r.Height, monitor, gap);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, (int)Math.Round(at.X), (int)Math.Round(at.Y), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>The enlarged pixels, with a crosshair stopping short of the centre cell so the
    /// inspected pixel keeps its true colour, and that cell outlined.</summary>
    private UIElement View()
    {
        const double viewW = SourceWidth * Zoom, viewH = SourceHeight * Zoom;
        const double cx = CentreX * Zoom, cy = CentreY * Zoom;
        var marks = new Canvas();
        AddMark(marks, cx, 0, CrossWidth, cy, CrossBrush, null);
        AddMark(marks, cx, cy + Zoom, CrossWidth, viewH - cy - Zoom, CrossBrush, null);
        AddMark(marks, 0, cy, cx, CrossWidth, CrossBrush, null);
        AddMark(marks, cx + Zoom, cy, viewW - cx - Zoom, CrossWidth, CrossBrush, null);
        AddMark(marks, cx, cy, Zoom, Zoom, null, Brushes.White);

        var view = new Grid { Width = viewW, Height = viewH };
        var pixels = new Rectangle { Fill = _pixels };
        RenderOptions.SetBitmapScalingMode(pixels, BitmapScalingMode.NearestNeighbor);
        view.Children.Add(pixels);
        view.Children.Add(marks);
        return new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(1), Background = Brushes.Black, Child = view };
    }

    /// <summary>The position, the colour and the keys, as the capture's loupe has them.</summary>
    private UIElement Readout()
    {
        var colourRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        colourRow.Children.Add(_swatch);
        colourRow.Children.Add(_colour);

        var rows = new StackPanel();
        _position.HorizontalAlignment = HorizontalAlignment.Center;
        rows.Children.Add(_position);
        rows.Children.Add(colourRow);
        rows.Children.Add(Hint(Loc.T("mag.copy"), new Thickness(0, 4, 0, 0)));
        rows.Children.Add(Hint(Loc.T("mag.format"), new Thickness(0)));
        return new Border { Background = PanelBrush, Padding = new Thickness(8, 6, 8, 7), Width = SourceWidth * Zoom + 2, Child = rows };
    }

    private static void AddMark(Canvas canvas, double x, double y, double w, double h, Brush? fill, Brush? stroke)
    {
        var mark = new Rectangle { Width = Math.Max(0, w), Height = Math.Max(0, h), Fill = fill, Stroke = stroke, StrokeThickness = 1 };
        Canvas.SetLeft(mark, x);
        Canvas.SetTop(mark, y);
        canvas.Children.Add(mark);
    }

    private static TextBlock Text(double size, Brush foreground) =>
        new() { FontSize = size, FontFamily = Mono, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center };

    private static TextBlock Hint(string text, Thickness margin) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = HintBrush,
        Margin = margin,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
