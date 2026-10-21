using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenPaste.Capture;
using ScreenPaste.Input;
using ScreenPaste.Native;

namespace ScreenPaste.Settings;

/// <summary>
/// The settings window's shortcut rows: the global hotkeys (<see cref="GlobalHotkeyTable"/>),
/// the extra snip modes under "More hotkeys" with the delayed snip's delay, and the button to
/// every in-app key of the capture and the pins (<see cref="KeyBindingsWindow"/>). A global
/// hotkey used twice, or on an in-app key, is named under the rows as it is typed
/// (<see cref="ShortcutConflicts"/>). While a field has the keyboard the app's global hotkeys
/// are let go, so pressing one types it instead of running it. Split out so the settings
/// window does not grow with every hotkey.
/// </summary>
internal sealed class ShortcutSettingsSection
{
    private readonly AppSettings _settings;
    private readonly Action<bool> _holdHotkeys;
    private readonly List<(GlobalHotkey Hotkey, TextBox Box)> _global = new();
    private readonly HashSet<TextBox> _boxes = new();
    private readonly TextBlock _warning;
    private TextBox _delay = null!;

    /// <param name="holdHotkeys">true while a hotkey field has the keyboard, false after.</param>
    public ShortcutSettingsSection(AppSettings settings, Action<bool> holdHotkeys)
    {
        _settings = settings;
        _holdHotkeys = holdHotkeys;
        _warning = new TextBlock
        {
            Foreground = SettingsWindow.WarningBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed,
        };
    }

    public void AddTo(Panel body)
    {
        body.Children.Add(SettingsWindow.Header(Loc.T("set.hotkeys")));
        body.Children.Add(SettingsWindow.Hint(Loc.T("set.hotkeyHint")));
        foreach (var hotkey in GlobalHotkeyTable.All.Where(h => !h.IsExtra)) body.Children.Add(GlobalRow(hotkey));
        AddMoreHotkeys(body);

        body.Children.Add(SettingsWindow.Header(Loc.T("set.inAppShortcuts")));
        body.Children.Add(SettingsWindow.Hint(Loc.T("set.inAppHint")));
        var edit = SettingsWindow.TextButton(Loc.T("keys.edit"), () => { });
        edit.HorizontalAlignment = HorizontalAlignment.Left;
        edit.Margin = new Thickness(0, 2, 0, 2);
        edit.Click += (_, _) => OpenKeyBindings(edit);
        body.Children.Add(SettingsWindow.Row("", edit));
        body.Children.Add(_warning);
        UpdateWarning();
    }

    public void Save(AppSettings s)
    {
        foreach (var (hotkey, box) in _global) hotkey.Set(s, GestureOf(box));
        if (ReadSeconds(_delay.Text) is { } seconds) s.SnipDelaySeconds = CustomSnipRequest.ClampDelay(seconds);
    }

    /// <summary>The extra snip modes, folded away unless one is assigned.</summary>
    private void AddMoreHotkeys(Panel body)
    {
        var more = new StackPanel();
        foreach (var hotkey in GlobalHotkeyTable.All.Where(h => h.IsExtra)) more.Children.Add(GlobalRow(hotkey));
        _delay = SettingsWindow.Themed(new TextBox
        {
            Text = CustomSnipRequest.ClampDelay(_settings.SnipDelaySeconds).ToString("0.##", CultureInfo.CurrentCulture),
            VerticalAlignment = VerticalAlignment.Center,
        });
        more.Children.Add(SettingsWindow.Row(Loc.T("set.snipDelay"), _delay));

        bool anyAssigned = GlobalHotkeyTable.All.Any(h => h.IsExtra && !string.IsNullOrWhiteSpace(h.Get(_settings)));
        more.Visibility = anyAssigned ? Visibility.Visible : Visibility.Collapsed;
        var toggle = SettingsWindow.TextButton(MoreLabel(anyAssigned), () => { });
        toggle.HorizontalAlignment = HorizontalAlignment.Left;
        toggle.Margin = new Thickness(0, 4, 0, 4);
        toggle.Click += (_, _) =>
        {
            bool show = more.Visibility != Visibility.Visible;
            more.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = MoreLabel(show);
        };
        body.Children.Add(SettingsWindow.Row("", toggle));
        body.Children.Add(more);
    }

    private static string MoreLabel(bool open) => Loc.T("set.moreHotkeys") + (open ? "  ▴" : "  ▾");

    /// <summary>The in-app keys, as a dialog over the settings window; saved on its own.</summary>
    private void OpenKeyBindings(FrameworkElement from)
    {
        var dialog = new KeyBindingsWindow(_settings, UpdateWarning, _holdHotkeys) { Owner = Window.GetWindow(from) };
        dialog.ShowDialog();
    }

    private FrameworkElement GlobalRow(GlobalHotkey hotkey)
    {
        var box = HotkeyBox(hotkey.Get(_settings));
        _global.Add((hotkey, box));
        return SettingsWindow.Row(Loc.T(hotkey.LabelKey), box);
    }

    private static double? ReadSeconds(string text)
    {
        text = text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)) return v;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : null;
    }

    // ---- conflicts ----

    private void UpdateWarning()
    {
        var conflicts = ShortcutConflicts.Global(
            _global.Select(g => (Loc.T(g.Hotkey.LabelKey), (string?)GestureOf(g.Box))),
            ShortcutMap.FromSettings(_settings), ShortcutText.Name);
        _warning.Text = string.Join("\n", conflicts.Select(c => "⚠ " + Describe(c)));
        _warning.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>One warning line; the in-app keys window words its own the same way.</summary>
    internal static string Describe(ShortcutConflict conflict)
    {
        var names = string.Join(", ", conflict.Names);
        var key = conflict.Kind switch
        {
            ShortcutConflictKind.GlobalDuplicate => "conflict.global",
            ShortcutConflictKind.GlobalTakesInAppKey => "conflict.globalInApp",
            _ => "conflict.inApp",
        };
        // Named as the fields show it ("Shift+1" for "!"); the stored text is what the window's outlines match.
        var shown = ShortcutKey.Parse(conflict.Key) is { } k ? ShortcutText.Key(k) : conflict.Key;
        return Loc.T(key, shown, names);
    }

    // ---- key-capture hotkey field: focus it and press the combo ----

    private TextBox HotkeyBox(string? value)
    {
        var tb = SettingsWindow.Themed(new TextBox { IsReadOnly = true, VerticalAlignment = VerticalAlignment.Center });
        SetGesture(tb, value ?? "");
        InputMethod.SetIsInputMethodEnabled(tb, false);   // stop IME from eating keys
        _boxes.Add(tb);
        tb.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey
                    : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (IsModifierKey(key)) { e.Handled = true; return; }
            if (key == Key.Escape) return;
            if (key is Key.Back or Key.Delete) { SetGesture(tb, ""); e.Handled = true; return; }

            var g = BuildGesture(Keyboard.Modifiers, key);
            if (g != null) SetGesture(tb, g);
            e.Handled = true;
        };
        tb.TextChanged += (_, _) => UpdateWarning();
        tb.GotKeyboardFocus += (_, _) => _holdHotkeys(true);
        // Moving to the next field keeps them let go; anywhere else takes them back.
        tb.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is TextBox next && _boxes.Contains(next)) return;
            _holdHotkeys(false);
        };
        return tb;
    }

    // The field shows "Shift+[" (ShortcutText.Gesture); its Tag holds what settings.json gets
    // and RegisterHotKey reads ("Shift+OemOpenBrackets"). Tag first: Text's change re-checks clashes.
    internal static void SetGesture(TextBox box, string gesture)
    {
        box.Tag = gesture.Trim();
        box.Text = ShortcutText.Gesture(gesture.Trim());
    }

    private static string GestureOf(TextBox box) => box.Tag as string ?? "";

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
}
