using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using Forms = System.Windows.Forms;

namespace ScreenPaste.Settings;

/// <summary>GUI editor for every AppSettings option. Saves + applies on confirm.</summary>
public sealed class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private readonly Action _onApplied;

    // Hotkeys
    private TextBox _capture = null!, _undo = null!, _redo = null!, _copy = null!, _save = null!, _quickSave = null!;
    // Appearance / startup / output
    private ComboBox _theme = null!;
    private CheckBox _startup = null!;
    private TextBox _saveDir = null!;
    // Pen / highlighter
    private Slider _penWidth = null!, _penOpacity = null!, _hlWidth = null!, _hlOpacity = null!;
    private Color _penColor, _hlColor;
    private Button _penSwatch = null!, _hlSwatch = null!;
    // Blur
    private Slider _gauss = null!, _mosaic = null!;
    // Text
    private ComboBox _font = null!;
    private Slider _textSize = null!;
    private CheckBox _bold = null!, _italic = null!, _strike = null!;
    private Color _textColor;
    private Button _textSwatch = null!;
    // Shape
    private ComboBox _shapeKind = null!;
    private CheckBox _shapeFilled = null!;
    private Slider _shapeWidth = null!;
    private Color _shapeColor;
    private Button _shapeSwatch = null!;

    public SettingsWindow(AppSettings settings, Action onApplied)
    {
        _s = settings;
        _onApplied = onApplied;

        Title = "ScreenPaste 設定";
        Width = 480;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;
        ResizeMode = ResizeMode.CanResize;

        _penColor = Parse(_s.PenColor, Colors.Red);
        _hlColor = Parse(_s.HighlighterColor, Colors.Yellow);
        _textColor = Parse(_s.TextColor, Colors.Red);
        _shapeColor = Parse(_s.ShapeColor, Colors.Red);

        var body = new StackPanel { Margin = new Thickness(16) };

        body.Children.Add(Header("熱鍵"));
        _capture = HotkeyBox(_s.CaptureHotkey); body.Children.Add(Row("截圖", _capture));
        _undo = HotkeyBox(_s.UndoHotkey); body.Children.Add(Row("復原", _undo));
        _redo = HotkeyBox(_s.RedoHotkey); body.Children.Add(Row("重做", _redo));
        _copy = HotkeyBox(_s.CopyHotkey); body.Children.Add(Row("複製", _copy));
        _save = HotkeyBox(_s.SaveHotkey); body.Children.Add(Row("儲存", _save));
        _quickSave = HotkeyBox(_s.QuickSaveHotkey); body.Children.Add(Row("快速儲存", _quickSave));

        body.Children.Add(Header("外觀與啟動"));
        _theme = Combo(new[] { "System", "Light", "Dark" }, _s.Theme);
        body.Children.Add(Row("主題", _theme));
        _startup = new CheckBox { IsChecked = _s.RunAtStartup, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.ForegroundBrush };
        body.Children.Add(Row("開機自動啟動", _startup));

        body.Children.Add(Header("儲存"));
        _saveDir = new TextBox { Text = _s.SaveDirectory, VerticalAlignment = VerticalAlignment.Center };
        var browse = TextButton("瀏覽…", BrowseFolder);
        var dirPanel = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        dirPanel.Children.Add(browse);
        dirPanel.Children.Add(_saveDir);
        body.Children.Add(Row("預設資料夾", dirPanel));

        body.Children.Add(Header("麥克筆"));
        _penWidth = Sld(1, 40, _s.PenWidth); body.Children.Add(Row("粗細", _penWidth));
        _penOpacity = Sld(0.1, 1, _s.PenOpacity); body.Children.Add(Row("透明度", _penOpacity));
        _penSwatch = Swatch(_penColor, () => OpenRgb(_penSwatch, () => _penColor, c => _penColor = c, _penOpacity));
        body.Children.Add(Row("顏色", _penSwatch));

        body.Children.Add(Header("螢光筆"));
        _hlWidth = Sld(1, 40, _s.HighlighterWidth); body.Children.Add(Row("粗細", _hlWidth));
        _hlOpacity = Sld(0.1, 1, _s.HighlighterOpacity); body.Children.Add(Row("透明度", _hlOpacity));
        _hlSwatch = Swatch(_hlColor, () => OpenRgb(_hlSwatch, () => _hlColor, c => _hlColor = c, _hlOpacity));
        body.Children.Add(Row("顏色", _hlSwatch));

        body.Children.Add(Header("模糊"));
        _gauss = Sld(2, 40, _s.GaussianStrength); body.Children.Add(Row("高斯程度", _gauss));
        _mosaic = Sld(2, 40, _s.MosaicStrength); body.Children.Add(Row("馬賽克程度", _mosaic));

        body.Children.Add(Header("文字"));
        _font = FontCombo(_s.TextFont); body.Children.Add(Row("字體", _font));
        _textSize = Sld(10, 96, _s.TextSize); body.Children.Add(Row("大小", _textSize));
        var styles = new StackPanel { Orientation = Orientation.Horizontal };
        _bold = Chk("粗體", _s.TextBold); _italic = Chk("斜體", _s.TextItalic); _strike = Chk("刪除線", _s.TextStrikethrough);
        styles.Children.Add(_bold); styles.Children.Add(_italic); styles.Children.Add(_strike);
        body.Children.Add(Row("樣式", styles));
        _textSwatch = Swatch(_textColor, () => OpenArgb(_textSwatch, () => _textColor, c => _textColor = c));
        body.Children.Add(Row("顏色", _textSwatch));

        body.Children.Add(Header("形狀"));
        _shapeKind = Combo(new[] { "Rectangle", "RoundedRectangle", "Ellipse" }, _s.ShapeKind);
        body.Children.Add(Row("形狀", _shapeKind));
        _shapeFilled = new CheckBox { IsChecked = _s.ShapeFilled, Content = "填滿", Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(Row("樣式", _shapeFilled));
        _shapeWidth = Sld(1, 20, _s.ShapeWidth); body.Children.Add(Row("線條粗細", _shapeWidth));
        _shapeSwatch = Swatch(_shapeColor, () => OpenArgb(_shapeSwatch, () => _shapeColor, c => _shapeColor = c));
        body.Children.Add(Row("顏色", _shapeSwatch));

        // Buttons
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(TextButton("儲存", Save, isDefault: true));
        buttons.Children.Add(TextButton("取消", Close));
        body.Children.Add(buttons);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body };
    }

    // ---------------------------------------------------------- persistence ---

    private void Save()
    {
        _s.CaptureHotkey = _capture.Text.Trim();
        _s.UndoHotkey = _undo.Text.Trim();
        _s.RedoHotkey = _redo.Text.Trim();
        _s.CopyHotkey = _copy.Text.Trim();
        _s.SaveHotkey = _save.Text.Trim();
        _s.QuickSaveHotkey = _quickSave.Text.Trim();

        _s.Theme = (string)((ComboBoxItem)_theme.SelectedItem).Content;
        _s.RunAtStartup = _startup.IsChecked == true;
        _s.SaveDirectory = _saveDir.Text.Trim();

        _s.PenWidth = _penWidth.Value;
        _s.PenOpacity = _penOpacity.Value;
        _s.PenColor = Hex(_penColor);
        _s.HighlighterWidth = _hlWidth.Value;
        _s.HighlighterOpacity = _hlOpacity.Value;
        _s.HighlighterColor = Hex(_hlColor);

        _s.GaussianStrength = _gauss.Value;
        _s.MosaicStrength = _mosaic.Value;

        _s.TextFont = _font.Text;
        _s.TextSize = _textSize.Value;
        _s.TextBold = _bold.IsChecked == true;
        _s.TextItalic = _italic.IsChecked == true;
        _s.TextStrikethrough = _strike.IsChecked == true;
        _s.TextColor = Hex(_textColor);

        _s.ShapeKind = (string)((ComboBoxItem)_shapeKind.SelectedItem).Content;
        _s.ShapeFilled = _shapeFilled.IsChecked == true;
        _s.ShapeWidth = _shapeWidth.Value;
        _s.ShapeColor = Hex(_shapeColor);

        _s.Save();
        _onApplied();
        Close();
    }

    private void BrowseFolder()
    {
        using var dlg = new Forms.FolderBrowserDialog { SelectedPath = _saveDir.Text };
        if (dlg.ShowDialog() == Forms.DialogResult.OK) _saveDir.Text = dlg.SelectedPath;
    }

    // ---- colour picker integration (anchored to the swatch) ----
    private void OpenRgb(Button swatch, Func<Color> get, Action<Color> set, Slider opacitySlider)
    {
        var dlg = new ColorPickerWindow(get(), opacitySlider.Value, ScreenRect(swatch)) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        set(dlg.SelectedColor);
        opacitySlider.Value = dlg.SelectedOpacity;
        swatch.Background = new SolidColorBrush(dlg.SelectedColor);
    }

    private void OpenArgb(Button swatch, Func<Color> get, Action<Color> set)
    {
        var cur = get();
        var dlg = new ColorPickerWindow(cur, cur.A / 255.0, ScreenRect(swatch)) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var a = (byte)Math.Round(dlg.SelectedOpacity * 255);
        var argb = Color.FromArgb(a, dlg.SelectedColor.R, dlg.SelectedColor.G, dlg.SelectedColor.B);
        set(argb);
        swatch.Background = new SolidColorBrush(Color.FromRgb(argb.R, argb.G, argb.B));
    }

    private static Rect ScreenRect(FrameworkElement el)
    {
        var tl = el.PointToScreen(new Point(0, 0));
        var br = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
        return new Rect(tl, br);
    }

    // ------------------------------------------------------------- widgets ---

    private static TextBlock Header(string t) => new()
    {
        Text = t,
        FontWeight = FontWeights.Bold,
        FontSize = 14,
        Foreground = new SolidColorBrush(Theme.Accent),
        Margin = new Thickness(0, 12, 0, 6),
    };

    private static FrameworkElement Row(string label, FrameworkElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock { Text = label, Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(control);
        return grid;
    }

    private static TextBox HotkeyBox(string val) => new() { Text = val, VerticalAlignment = VerticalAlignment.Center };

    private static Slider Sld(double min, double max, double val) => new()
    {
        Minimum = min, Maximum = max, Value = Math.Clamp(val, min, max),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private CheckBox Chk(string text, bool val) => new()
    {
        Content = text, IsChecked = val, Margin = new Thickness(0, 0, 12, 0),
        Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center,
    };

    private static ComboBox Combo(string[] items, string selected)
    {
        var cb = new ComboBox { VerticalAlignment = VerticalAlignment.Center };
        foreach (var it in items) cb.Items.Add(new ComboBoxItem { Content = it });
        cb.SelectedIndex = Math.Max(0, Array.FindIndex(items, i => string.Equals(i, selected, StringComparison.OrdinalIgnoreCase)));
        return cb;
    }

    private static ComboBox FontCombo(string selected)
    {
        var cb = new ComboBox { VerticalAlignment = VerticalAlignment.Center, IsEditable = true };
        foreach (var f in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            cb.Items.Add(f);
        cb.Text = selected;
        return cb;
    }

    private Button Swatch(Color c, Action onClick)
    {
        var b = new Button
        {
            Width = 48, Height = 22, HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x88, 0x88, 0x88)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button TextButton(string text, Action onClick, bool isDefault = false)
    {
        var b = new Button { Content = text, MinWidth = 76, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4), IsDefault = isDefault };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Color Parse(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); } catch { return fallback; }
    }

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
