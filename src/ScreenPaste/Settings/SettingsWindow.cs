using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenPaste.Native;
using Forms = System.Windows.Forms;

namespace ScreenPaste.Settings;

/// <summary>
/// GUI for app-level settings: hotkeys, appearance, startup, save folder.
/// (Per-tool defaults are adjusted live in the editor toolbar and persist there.)
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private readonly Action _onApplied;

    private TextBox _capture = null!, _undo = null!, _redo = null!, _copy = null!, _save = null!, _quickSave = null!;
    private ComboBox _theme = null!;
    private CheckBox _startup = null!;
    private TextBox _saveDir = null!;

    public SettingsWindow(AppSettings settings, Action onApplied)
    {
        _s = settings;
        _onApplied = onApplied;

        Title = "ScreenPaste 設定";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(16) };

        body.Children.Add(Header("熱鍵"));
        body.Children.Add(new TextBlock
        {
            Text = "點選欄位後直接按下想要的按鍵組合；Delete 可清除。",
            Foreground = Theme.ForegroundBrush, FontSize = 11, Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 6),
        });
        _capture = HotkeyBox(_s.CaptureHotkey); body.Children.Add(Row("截圖", _capture));
        _undo = HotkeyBox(_s.UndoHotkey); body.Children.Add(Row("復原", _undo));
        _redo = HotkeyBox(_s.RedoHotkey); body.Children.Add(Row("重做", _redo));
        _copy = HotkeyBox(_s.CopyHotkey); body.Children.Add(Row("複製", _copy));
        _save = HotkeyBox(_s.SaveHotkey); body.Children.Add(Row("儲存", _save));
        _quickSave = HotkeyBox(_s.QuickSaveHotkey); body.Children.Add(Row("快速儲存", _quickSave));

        body.Children.Add(Header("外觀與啟動"));
        _theme = Combo(new[] { "System", "Light", "Dark" }, _s.Theme);
        body.Children.Add(Row("主題", _theme));
        _startup = new CheckBox { IsChecked = _s.RunAtStartup, Content = "開機時自動啟動", Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(Row("", _startup));

        body.Children.Add(Header("儲存"));
        _saveDir = Themed(new TextBox { Text = _s.SaveDirectory, VerticalAlignment = VerticalAlignment.Center });
        var browse = TextButton("瀏覽…", BrowseFolder);
        DockPanel.SetDock(browse, Dock.Right);
        var dirPanel = new DockPanel();
        dirPanel.Children.Add(browse);
        dirPanel.Children.Add(_saveDir);
        body.Children.Add(Row("預設資料夾", dirPanel));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(TextButton("儲存", Save, isDefault: true));
        buttons.Children.Add(TextButton("取消", Close));
        body.Children.Add(buttons);

        Content = body;
    }

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

        _s.Save();
        _onApplied();
        Close();
    }

    private void BrowseFolder()
    {
        using var dlg = new Forms.FolderBrowserDialog { SelectedPath = _saveDir.Text };
        if (dlg.ShowDialog() == Forms.DialogResult.OK) _saveDir.Text = dlg.SelectedPath;
    }

    // ---- key-capture hotkey field: focus it and press the combo ----

    private TextBox HotkeyBox(string val)
    {
        var tb = Themed(new TextBox
        {
            Text = val,
            IsReadOnly = true,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "按下想要的快速鍵組合",
        });
        InputMethod.SetIsInputMethodEnabled(tb, false);   // stop IME from eating keys
        tb.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey
                    : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (IsModifierKey(key)) { e.Handled = true; return; }   // wait for the real key
            if (key == Key.Escape) return;                          // let the dialog cancel
            if (key is Key.Back or Key.Delete) { tb.Text = ""; e.Handled = true; return; }

            var g = BuildGesture(Keyboard.Modifiers, key);
            if (g != null) tb.Text = g;
            e.Handled = true;
        };
        return tb;
    }

    private static bool IsModifierKey(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;

    /// <summary>Build a gesture string ("Ctrl+Shift+S", "F1") and validate it parses.</summary>
    private static string? BuildGesture(ModifierKeys mods, Key key)
    {
        var prefix = "";
        if (mods.HasFlag(ModifierKeys.Control)) prefix += "Ctrl+";
        if (mods.HasFlag(ModifierKeys.Alt)) prefix += "Alt+";
        if (mods.HasFlag(ModifierKeys.Shift)) prefix += "Shift+";
        if (mods.HasFlag(ModifierKeys.Windows)) prefix += "Win+";

        var candidate = prefix + key;
        return HotkeyGesture.Parse(candidate) != null ? candidate : null;
    }

    // ------------------------------------------------------------- widgets ---

    private static TextBlock Header(string t) => new()
    {
        Text = t, FontWeight = FontWeights.Bold, FontSize = 14,
        Foreground = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 12, 0, 6),
    };

    private static FrameworkElement Row(string label, FrameworkElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock { Text = label, Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(control);
        return grid;
    }

    private static TextBox Themed(TextBox tb)
    {
        tb.Background = Theme.ControlBgBrush;
        tb.Foreground = Theme.ForegroundBrush;
        tb.BorderBrush = Theme.ControlBorderBrush;
        tb.CaretBrush = Theme.ForegroundBrush;
        tb.Padding = new Thickness(4, 2, 4, 2);
        return tb;
    }

    private static ComboBox Combo(string[] items, string selected)
    {
        var cb = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Background = Theme.ControlBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ControlBorderBrush,
        };
        foreach (var it in items) cb.Items.Add(new ComboBoxItem { Content = it });
        cb.SelectedIndex = Math.Max(0, Array.FindIndex(items, i => string.Equals(i, selected, StringComparison.OrdinalIgnoreCase)));
        return cb;
    }

    private static Button TextButton(string text, Action onClick, bool isDefault = false)
    {
        var b = new Button
        {
            Content = text, MinWidth = 76, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4),
            IsDefault = isDefault,
            Background = Theme.ButtonBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ButtonBorderBrush,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
