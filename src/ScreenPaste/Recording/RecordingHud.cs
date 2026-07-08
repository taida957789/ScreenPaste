using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenPaste.Native;
using ScreenPaste.Settings;
using static ScreenPaste.Native.NativeMethods;

namespace ScreenPaste.Recording;

/// <summary>
/// On-screen recording indicator: a click-through red frame around the captured region and
/// a small bar (elapsed time + Stop) docked just outside it. Neither window is captured, since
/// the frame sits outside the recorded rect and the bar is positioned beyond it.
/// </summary>
public sealed class RecordingHud
{
    private const double BorderDip = 3.0;
    private const double Gap = 8.0;

    private readonly Int32Rect _region;   // physical px, screen coords
    private readonly VirtualScreen _vs;
    private readonly Window _frame = new();
    private readonly Window _bar = new();
    private readonly TextBlock _time = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();

    /// <summary>Raised (on the UI thread) when the user clicks Stop.</summary>
    public event Action? StopRequested;

    public RecordingHud(Int32Rect region)
    {
        _region = region;
        _vs = VirtualScreen.Current;
        BuildFrame();
        BuildBar();
    }

    public void Show()
    {
        _frame.Show();
        _bar.Show();
        _tick.Tick += (_, _) => UpdateTime();
        _tick.Start();
        UpdateTime();
    }

    public void Close()
    {
        _tick.Stop();
        _frame.Close();
        _bar.Close();
    }

    private void UpdateTime()
    {
        var t = _elapsed.Elapsed;
        _time.Text = $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }

    // ---- Red frame around the region (click-through) ----

    private void BuildFrame()
    {
        _frame.WindowStyle = WindowStyle.None;
        _frame.ResizeMode = ResizeMode.NoResize;
        _frame.ShowInTaskbar = false;
        _frame.Topmost = true;
        _frame.AllowsTransparency = true;
        _frame.Background = Brushes.Transparent;
        _frame.WindowStartupLocation = WindowStartupLocation.Manual;
        _frame.IsHitTestVisible = false;
        _frame.Content = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B)),
            Background = Brushes.Transparent,
        };

        _frame.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(_frame).Handle;
            uint dpiRaw = GetDpiForWindow(hwnd);
            double dpi = dpiRaw <= 0 ? 1.0 : dpiRaw / 96.0;
            int physT = (int)Math.Round(BorderDip * dpi);

            ((Border)_frame.Content).BorderThickness = new Thickness(physT / dpi);

            // Ex-styles: click-through + tool window (out of Alt+Tab).
            AddExStyle(hwnd, (int)(WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));

            // Sit the frame OUTSIDE the recorded rect so the border pixels are never captured.
            SetWindowPos(hwnd, HWND_TOPMOST,
                _region.X - physT, _region.Y - physT,
                _region.Width + 2 * physT, _region.Height + 2 * physT,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        };
    }

    // ---- Elapsed-time + Stop bar (clickable, positioned outside the region) ----

    private void BuildBar()
    {
        _bar.WindowStyle = WindowStyle.None;
        _bar.ResizeMode = ResizeMode.NoResize;
        _bar.ShowInTaskbar = false;
        _bar.Topmost = true;
        _bar.AllowsTransparency = true;
        _bar.Background = Brushes.Transparent;
        _bar.SizeToContent = SizeToContent.WidthAndHeight;
        _bar.WindowStartupLocation = WindowStartupLocation.Manual;

        var dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        _time.Foreground = Brushes.White;
        _time.FontSize = 14;
        _time.VerticalAlignment = VerticalAlignment.Center;
        _time.Text = "00:00";

        var stop = new Button
        {
            Content = Loc.T("rec.stop"),
            Margin = new Thickness(12, 0, 0, 0),
            Padding = new Thickness(12, 3, 12, 3),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B)),
            Cursor = Cursors.Hand,
        };
        stop.Click += (_, _) => StopRequested?.Invoke();

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(dot);
        panel.Children.Add(_time);
        panel.Children.Add(stop);

        _bar.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1E, 0x1E, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(19),   // pill
            Padding = new Thickness(16, 8, 10, 8),
            Child = panel,
        };

        _bar.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(_bar).Handle;
            AddExStyle(hwnd, (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        };

        // Position once laid out (need ActualWidth/Height for physical placement).
        _bar.Loaded += (_, _) => PositionBar();
    }

    private void PositionBar()
    {
        var hwnd = new WindowInteropHelper(_bar).Handle;
        uint dpiRaw = GetDpiForWindow(hwnd);
        double dpi = dpiRaw <= 0 ? 1.0 : dpiRaw / 96.0;

        int w = (int)Math.Round(_bar.ActualWidth * dpi);
        int h = (int)Math.Round(_bar.ActualHeight * dpi);

        // Prefer just below the region; flip above if it would fall off-screen.
        int x = _region.X;
        int y = _region.Y + _region.Height + (int)Math.Round(Gap * dpi);
        if (y + h > _vs.Y + _vs.Height)
            y = _region.Y - h - (int)Math.Round(Gap * dpi);

        x = Math.Clamp(x, _vs.X, _vs.X + _vs.Width - w);
        y = Math.Clamp(y, _vs.Y, _vs.Y + _vs.Height - h);

        SetWindowPos(hwnd, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static void AddExStyle(IntPtr hwnd, int extra)
    {
        int cur = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, cur | extra);
    }
}
