using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenPaste.Native;
using Forms = System.Windows.Forms;

namespace ScreenPaste.Settings;

/// <summary>
/// GUI for app-level settings: language, hotkeys, appearance, startup, save folder.
/// (Per-tool defaults are adjusted live in the editor toolbar and persist there.)
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private readonly Action _onApplied;
    private readonly Action _onCheckUpdates;

    private TextBox _capture = null!, _undo = null!, _redo = null!, _copy = null!, _save = null!, _quickSave = null!;
    private ComboBox _language = null!;
    private ComboBox _theme = null!;
    private CheckBox _startup = null!;
    private CheckBox _checkUpdate = null!;
    private TextBox _saveDir = null!;

    public SettingsWindow(AppSettings settings, Action onApplied, Action onCheckUpdates)
    {
        _s = settings;
        _onApplied = onApplied;
        _onCheckUpdates = onCheckUpdates;

        Title = Loc.T("set.title");
        Width = 470;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(16) };

        body.Children.Add(Header(Loc.T("set.appearance")));

        var langItems = new List<(string value, string display, string? flag)> { ("System", Loc.T("theme.system"), null) };
        foreach (var l in Loc.Languages) langItems.Add((l.Code, l.Native, l.FlagCode));
        _language = ValueCombo(langItems, _s.Language);
        body.Children.Add(Row(Loc.T("set.language"), _language));

        _theme = ValueCombo(new()
        {
            ("System", Loc.T("theme.system"), null),
            ("Light", Loc.T("theme.light"), null),
            ("Dark", Loc.T("theme.dark"), null),
        }, _s.Theme);
        body.Children.Add(Row(Loc.T("set.theme"), _theme));

        _startup = new CheckBox { IsChecked = _s.RunAtStartup, Content = Loc.T("set.startup"), Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(Row("", _startup));

        body.Children.Add(Header(Loc.T("set.updates")));
        _checkUpdate = new CheckBox { IsChecked = _s.CheckUpdateOnStartup, Content = Loc.T("set.checkStartup"), Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(Row("", _checkUpdate));
        var checkNow = TextButton(Loc.T("set.checkNow"), () => _onCheckUpdates());
        checkNow.HorizontalAlignment = HorizontalAlignment.Left;
        checkNow.Margin = new Thickness(0);
        body.Children.Add(Row("", checkNow));

        body.Children.Add(Header(Loc.T("set.hotkeys")));
        body.Children.Add(new TextBlock
        {
            Text = Loc.T("set.hotkeyHint"),
            Foreground = Theme.ForegroundBrush, FontSize = 11, Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
        });
        _capture = HotkeyBox(_s.CaptureHotkey); body.Children.Add(Row(Loc.T("set.capture"), _capture));
        _undo = HotkeyBox(_s.UndoHotkey); body.Children.Add(Row(Loc.T("action.undo"), _undo));
        _redo = HotkeyBox(_s.RedoHotkey); body.Children.Add(Row(Loc.T("action.redo"), _redo));
        _copy = HotkeyBox(_s.CopyHotkey); body.Children.Add(Row(Loc.T("action.copy"), _copy));
        _save = HotkeyBox(_s.SaveHotkey); body.Children.Add(Row(Loc.T("action.save"), _save));
        _quickSave = HotkeyBox(_s.QuickSaveHotkey); body.Children.Add(Row(Loc.T("set.quickSave"), _quickSave));

        body.Children.Add(Header(Loc.T("set.saveSection")));
        _saveDir = Themed(new TextBox { Text = _s.SaveDirectory, VerticalAlignment = VerticalAlignment.Center });
        var browse = TextButton(Loc.T("set.browse"), BrowseFolder);
        DockPanel.SetDock(browse, Dock.Right);
        var dirPanel = new DockPanel();
        dirPanel.Children.Add(browse);
        dirPanel.Children.Add(_saveDir);
        body.Children.Add(Row(Loc.T("set.saveFolder"), dirPanel));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(TextButton(Loc.T("common.save"), Save, isDefault: true));
        buttons.Children.Add(TextButton(Loc.T("common.cancel"), Close));
        body.Children.Add(buttons);

        Content = body;
    }

    private void Save()
    {
        _s.Language = ComboValue(_language);
        _s.Theme = ComboValue(_theme);
        _s.RunAtStartup = _startup.IsChecked == true;
        _s.CheckUpdateOnStartup = _checkUpdate.IsChecked == true;
        _s.SaveDirectory = _saveDir.Text.Trim();

        _s.CaptureHotkey = _capture.Text.Trim();
        _s.UndoHotkey = _undo.Text.Trim();
        _s.RedoHotkey = _redo.Text.Trim();
        _s.CopyHotkey = _copy.Text.Trim();
        _s.SaveHotkey = _save.Text.Trim();
        _s.QuickSaveHotkey = _quickSave.Text.Trim();

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
        var tb = Themed(new TextBox { Text = val, IsReadOnly = true, VerticalAlignment = VerticalAlignment.Center });
        InputMethod.SetIsInputMethodEnabled(tb, false);   // stop IME from eating keys
        tb.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey
                    : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (IsModifierKey(key)) { e.Handled = true; return; }
            if (key == Key.Escape) return;
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
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

    /// <summary>Combo whose items carry a value in Tag, an optional flag icon, and localized text.</summary>
    private static ComboBox ValueCombo(List<(string value, string display, string? flag)> items, string selected)
    {
        var cb = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Background = Theme.ControlBgBrush,
            Foreground = Theme.ForegroundBrush,
            BorderBrush = Theme.ControlBorderBrush,
        };
        foreach (var (value, display, flag) in items)
        {
            object content;
            if (flag != null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new Image { Source = FlagIcons.Get(flag), Width = 20, Height = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = display, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.ForegroundBrush });
                content = sp;
            }
            else
            {
                content = new TextBlock { Text = display, Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center };
            }
            var item = new ComboBoxItem { Content = content, Tag = value };
            cb.Items.Add(item);
            if (value.Equals(selected, StringComparison.OrdinalIgnoreCase)) cb.SelectedItem = item;
        }
        if (cb.SelectedItem == null && cb.Items.Count > 0) cb.SelectedIndex = 0;
        return cb;
    }

    private static string ComboValue(ComboBox cb) => (string)((ComboBoxItem)cb.SelectedItem).Tag!;

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
