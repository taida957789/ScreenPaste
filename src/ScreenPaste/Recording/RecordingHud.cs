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
using Forms = System.Windows.Forms;
using DPoint = System.Drawing.Point;

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

    private Int32Rect _region;            // physical px, screen coords; movable by dragging
    private readonly VirtualScreen _vs;
    private readonly Window _frame = new();
    private readonly Window _bar = new();
    private readonly TextBlock _time = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private int _physBorder = 3;          // frame border in physical px (set at DPI resolve)
    private bool _dragging;
    private DPoint _dragStartCursor;
    private Int32Rect _dragStartRegion;

    /// <summary>Raised (on the UI thread) when the user clicks Stop.</summary>
    public event Action? StopRequested;

    /// <summary>Raised while the user drags the red frame: new region origin (physical px).</summary>
    public event Action<int, int>? RegionMoved;

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

    // ---- Red frame around the region (centre is click-through; the border drags) ----

    private void BuildFrame()
    {
        _frame.WindowStyle = WindowStyle.None;
        _frame.ResizeMode = ResizeMode.NoResize;
        _frame.ShowInTaskbar = false;
        _frame.Topmost = true;
        _frame.AllowsTransparency = true;
        _frame.Background = Brushes.Transparent;
        _frame.WindowStartupLocation = WindowStartupLocation.Manual;

        // Null background: only the border ring hit-tests, so the recorded app stays
        // fully interactive through the middle while the red edge can be grabbed to
        // move the region mid-recording.
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B)),
            Background = null,
            Cursor = Cursors.SizeAll,
        };
        border.MouseLeftButtonDown += Frame_DragStart;
        border.MouseMove += Frame_DragMove;
        border.MouseLeftButtonUp += Frame_DragEnd;
        _frame.Content = border;

        _frame.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(_frame).Handle;
            uint dpiRaw = GetDpiForWindow(hwnd);
            double dpi = dpiRaw <= 0 ? 1.0 : dpiRaw / 96.0;
            _physBorder = (int)Math.Round(BorderDip * dpi);

            border.BorderThickness = new Thickness(_physBorder / dpi);

            // Tool window (out of Alt+Tab), no focus stealing — but NOT click-through:
            // the border ring must receive the drag.
            AddExStyle(hwnd, (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));

            PositionFrame();
        };
    }

    /// <summary>Sit the frame OUTSIDE the recorded rect so its pixels are never captured.</summary>
    private void PositionFrame()
    {
        var hwnd = new WindowInteropHelper(_frame).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST,
            _region.X - _physBorder, _region.Y - _physBorder,
            _region.Width + 2 * _physBorder, _region.Height + 2 * _physBorder,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void Frame_DragStart(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStartCursor = Forms.Cursor.Position;   // physical px, DPI-safe
        _dragStartRegion = _region;
        (sender as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void Frame_DragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var cur = Forms.Cursor.Position;
        int nx = Math.Clamp(_dragStartRegion.X + (cur.X - _dragStartCursor.X),
            _vs.X, _vs.X + _vs.Width - _region.Width);
        int ny = Math.Clamp(_dragStartRegion.Y + (cur.Y - _dragStartCursor.Y),
            _vs.Y, _vs.Y + _vs.Height - _region.Height);
        if (nx == _region.X && ny == _region.Y) return;

        _region = new Int32Rect(nx, ny, _region.Width, _region.Height);
        PositionFrame();
        PositionBar();
        RegionMoved?.Invoke(nx, ny);
    }

    private void Frame_DragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        (sender as UIElement)?.ReleaseMouseCapture();
        e.Handled = true;
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
