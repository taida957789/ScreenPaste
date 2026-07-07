using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Editor;

/// <summary>
/// A small colour picker: hex entry, R/G/B and opacity sliders, live preview.
/// It anchors to the toolbar (below it, or above when there's no room below).
/// </summary>
public sealed class ColorPickerWindow : Window
{
    public Color SelectedColor { get; private set; }     // RGB (alpha ignored)
    public double SelectedOpacity { get; private set; }  // 0..1

    private readonly Slider _r, _g, _b, _opacity;
    private readonly TextBox _hex;
    private readonly Border _preview;
    private readonly Rect _anchorScreenPx;
    private bool _updating;

    public ColorPickerWindow(Color initial, double initialOpacity, Rect anchorScreenPx)
    {
        SelectedColor = initial;
        SelectedOpacity = Math.Clamp(initialOpacity, 0, 1);
        _anchorScreenPx = anchorScreenPx;

        Title = "選擇顏色";
        Width = 300;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var root = new StackPanel { Margin = new Thickness(14) };

        _preview = new Border
        {
            Height = 40,
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x88, 0x88, 0x88)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
        };
        root.Children.Add(_preview);

        var hexRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        hexRow.Children.Add(MakeLabel("Hex", 44));
        _hex = new TextBox { FontFamily = new FontFamily("Consolas"), Padding = new Thickness(4, 2, 4, 2) };
        _hex.TextChanged += (_, _) => UpdateFromHex();
        hexRow.Children.Add(_hex);
        root.Children.Add(hexRow);

        _r = MakeChannel(root, "R", initial.R, 255);
        _g = MakeChannel(root, "G", initial.G, 255);
        _b = MakeChannel(root, "B", initial.B, 255);
        _opacity = MakeChannel(root, "透明度", SelectedOpacity * 100, 100);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var ok = new Button { Content = "確定", Width = 72, Margin = new Thickness(6, 0, 0, 0), IsDefault = true };
        var cancel = new Button { Content = "取消", Width = 72, Margin = new Thickness(6, 0, 0, 0), IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = root;

        SyncFromSliders();
        SourceInitialized += (_, _) => PositionNearAnchor();
    }

    private Slider MakeChannel(Panel parent, string name, double value, double max)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(MakeLabel(name, 44));
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = max,
            Value = value,
            VerticalAlignment = VerticalAlignment.Center,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
        };
        slider.ValueChanged += (_, _) => SyncFromSliders();
        row.Children.Add(slider);
        parent.Children.Add(row);
        return slider;
    }

    private TextBlock MakeLabel(string text, double width) => new()
    {
        Text = text,
        Width = width,
        Foreground = Theme.ForegroundBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void SyncFromSliders()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var rgb = Color.FromRgb((byte)_r.Value, (byte)_g.Value, (byte)_b.Value);
            SelectedColor = rgb;
            SelectedOpacity = _opacity.Value / 100.0;
            _hex.Text = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";
            UpdatePreview();
        }
        finally { _updating = false; }
    }

    private void UpdateFromHex()
    {
        if (_updating) return;
        if (!TryParseHex(_hex.Text, out var c)) return;
        _updating = true;
        try
        {
            _r.Value = c.R;
            _g.Value = c.G;
            _b.Value = c.B;
            SelectedColor = c;
            UpdatePreview();
        }
        finally { _updating = false; }
    }

    private void UpdatePreview()
    {
        var a = (byte)Math.Round(SelectedOpacity * 255);
        _preview.Background = new SolidColorBrush(Color.FromArgb(a, SelectedColor.R, SelectedColor.G, SelectedColor.B));
    }

    /// <summary>Place below the toolbar, or above it if there isn't room below.</summary>
    private void PositionNearAnchor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        double scale = dpi <= 0 ? 1.0 : dpi / 96.0;

        // Force a layout pass so ActualWidth/Height are known.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(new Size(ActualWidth, ActualHeight)));
        SizeToContent = SizeToContent.Manual;

        int w = (int)Math.Round(ActualWidth * scale);
        int h = (int)Math.Round(ActualHeight * scale);
        var vs = VirtualScreen.Current;
        int gap = (int)Math.Round(8 * scale);

        int x = (int)Math.Round(_anchorScreenPx.X);
        int yBelow = (int)Math.Round(_anchorScreenPx.Bottom) + gap;
        int yAbove = (int)Math.Round(_anchorScreenPx.Y) - gap - h;

        int y = (yBelow + h <= vs.Y + vs.Height) ? yBelow
              : (yAbove >= vs.Y ? yAbove : yBelow);

        x = Math.Clamp(x, vs.X, Math.Max(vs.X, vs.X + vs.Width - w));
        y = Math.Clamp(y, vs.Y, Math.Max(vs.Y, vs.Y + vs.Height - h));

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, w, h,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>Accepts "#RGB", "#RRGGBB", "#AARRGGBB", with or without the leading '#'.</summary>
    private static bool TryParseHex(string text, out Color color)
    {
        color = Colors.Black;
        var s = text.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length == 8) s = s.Substring(2);   // drop alpha; opacity is a separate control
        if (s.Length != 6) return false;
        if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return true;
    }
}
