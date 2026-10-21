using System.Windows;
using System.Windows.Controls;
using ScreenPaste.Input;

namespace ScreenPaste.Settings;

/// <summary>
/// Every in-app key of the capture and the pins (<see cref="ShortcutActions"/>), grouped as the
/// table lists them, two keys per action. Click a field and press a key; the clear button
/// unbinds it, the row's reset button gives the action its Snipaste keys again. Keys two
/// actions share where both can run, or that a global hotkey takes, are named under the list
/// as they are set. Saving writes only what differs from the defaults (<see cref="ShortcutMap.SaveTo"/>).
/// </summary>
internal sealed class KeyBindingsWindow : Window
{
    private const int Slots = 2;
    private const double LabelWidth = 250;
    private const double BoxWidth = 150;

    private readonly AppSettings _settings;
    private readonly Action _onSaved;
    private readonly Action<bool> _holdHotkeys;
    private readonly Dictionary<string, KeyCaptureBox[]> _rows = new();
    private readonly Dictionary<string, ShortcutKey[]> _extras = new();   // keys past the two fields (a hand-edited file), kept as they are
    private readonly HashSet<TextBox> _boxes = new();
    private readonly TextBlock _warning;

    /// <param name="onSaved">Run after the keys are saved (the settings window redoes its warning).</param>
    /// <param name="holdHotkeys">true while a field reads keys (the app lets its global hotkeys
    /// go, so F3 can be bound), false after.</param>
    public KeyBindingsWindow(AppSettings settings, Action onSaved, Action<bool> holdHotkeys)
    {
        _settings = settings;
        _onSaved = onSaved;
        _holdHotkeys = holdHotkeys;
        Closed += (_, _) => holdHotkeys(false);

        Title = Loc.T("keys.title");
        Width = 700;
        Height = 720;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        _warning = new TextBlock
        {
            Foreground = SettingsWindow.WarningBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed,
        };

        var list = new StackPanel { Margin = new Thickness(16, 0, 16, 8) };
        AddArea(list, ShortcutArea.Capture, "keys.area.capture");
        AddArea(list, ShortcutArea.Pin, "keys.area.pin");

        var root = new DockPanel { Margin = new Thickness(0, 12, 0, 12) };
        var top = SettingsWindow.Hint(Loc.T("keys.hint"));
        top.Margin = new Thickness(16, 0, 16, 8);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = Footer();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });
        Content = root;

        UpdateWarning();
    }

    private void AddArea(Panel list, ShortcutArea area, string headerKey)
    {
        list.Children.Add(SettingsWindow.Header(Loc.T(headerKey)));
        var keys = ShortcutMap.FromSettings(_settings);
        string? group = null;
        foreach (var action in ShortcutActions.All.Where(a => a.Area == area))
        {
            if (action.Group != group)
            {
                group = action.Group;
                list.Children.Add(new TextBlock
                {
                    Text = Loc.T(group), FontWeight = FontWeights.SemiBold, Opacity = 0.8,
                    Foreground = Theme.ForegroundBrush, Margin = new Thickness(0, 8, 0, 2),
                });
            }
            list.Children.Add(Row(action, keys.For(action.Id)));
        }
    }

    private FrameworkElement Row(ShortcutAction action, IReadOnlyList<ShortcutKey> keys)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        for (int i = 0; i < Slots; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BoxWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = ShortcutText.Name(action), Foreground = Theme.ForegroundBrush, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        };
        grid.Children.Add(label);

        var slots = new KeyCaptureBox[Slots];
        for (int i = 0; i < Slots; i++)
        {
            var slot = new KeyCaptureBox(i < keys.Count ? keys[i] : null);
            slot.Changed += UpdateWarning;
            WatchFocus(slot.Box);
            slots[i] = slot;
            var cell = SlotCell(slot);
            Grid.SetColumn(cell, 1 + i);
            grid.Children.Add(cell);
        }
        _rows[action.Id] = slots;
        _extras[action.Id] = keys.Skip(Slots).ToArray();

        var reset = SmallButton("↺", Loc.T("keys.reset"), () => SetRow(action.Id, action.DefaultKeys));
        Grid.SetColumn(reset, 1 + Slots);
        grid.Children.Add(reset);
        return grid;
    }

    /// <summary>A key field with its clear button.</summary>
    private static FrameworkElement SlotCell(KeyCaptureBox slot)
    {
        var clear = SmallButton("✕", Loc.T("keys.clear"), () => slot.Set(null));
        DockPanel.SetDock(clear, Dock.Right);
        var cell = new DockPanel { Margin = new Thickness(0, 0, 6, 0) };
        cell.Children.Add(clear);
        cell.Children.Add(slot.Box);
        return cell;
    }

    private FrameworkElement Footer()
    {
        var panel = new DockPanel { Margin = new Thickness(16, 8, 16, 0) };
        DockPanel.SetDock(_warning, Dock.Top);
        panel.Children.Add(_warning);

        var resetAll = SettingsWindow.TextButton(Loc.T("keys.resetAll"), ResetAll);
        resetAll.Margin = new Thickness(0, 12, 0, 0);
        DockPanel.SetDock(resetAll, Dock.Left);
        panel.Children.Add(resetAll);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        // Not IsDefault / IsCancel: Enter and Esc are keys to bind here, not to close the window.
        buttons.Children.Add(SettingsWindow.TextButton(Loc.T("common.save"), Save));
        buttons.Children.Add(SettingsWindow.TextButton(Loc.T("common.cancel"), Close));
        panel.Children.Add(buttons);
        return panel;
    }

    private static Button SmallButton(string glyph, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = glyph, ToolTip = tip, Width = 24, Padding = new Thickness(0), Margin = new Thickness(2, 0, 0, 0),
            Background = Theme.ButtonBgBrush, Foreground = Theme.ForegroundBrush, BorderBrush = Theme.ButtonBorderBrush,
            Focusable = false,   // a click must not take the focus from a field being set
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void SetRow(string id, IReadOnlyList<ShortcutKey> keys)
    {
        var slots = _rows[id];
        _extras[id] = keys.Skip(Slots).ToArray();
        for (int i = 0; i < Slots; i++) slots[i].Set(i < keys.Count ? keys[i] : null);
    }

    private void ResetAll()
    {
        foreach (var action in ShortcutActions.All) SetRow(action.Id, action.DefaultKeys);
    }

    /// <summary>The keys as the fields hold them now.</summary>
    private ShortcutMap CurrentMap()
    {
        var map = ShortcutMap.Defaults;
        foreach (var (id, slots) in _rows)
            map = map.With(id, slots.Select(s => s.Value).OfType<ShortcutKey>().Concat(_extras[id]));
        return map;
    }

    private void UpdateWarning()
    {
        var map = CurrentMap();
        var global = GlobalHotkeyTable.All.Select(h => (Loc.T(h.LabelKey), (string?)h.Get(_settings)));
        var conflicts = ShortcutConflicts.InApp(map, ShortcutText.Name)
            .Concat(ShortcutConflicts.Global(global, map, ShortcutText.Name)
                .Where(c => c.Kind == ShortcutConflictKind.GlobalTakesInAppKey))
            .ToList();
        _warning.Text = string.Join("\n", conflicts.Select(c => "⚠ " + ShortcutSettingsSection.Describe(c)));
        _warning.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MarkClashes(conflicts);
    }

    /// <summary>Outline the fields whose key a warning names.</summary>
    private void MarkClashes(IReadOnlyList<ShortcutConflict> conflicts)
    {
        var clashing = conflicts.Select(c => c.Key).ToHashSet();
        foreach (var slot in _rows.Values.SelectMany(s => s))
            slot.Box.BorderBrush = slot.Value is { } key && clashing.Contains(key.ToString())
                ? SettingsWindow.WarningBrush
                : Theme.ControlBorderBrush;
    }

    private void Save()
    {
        CurrentMap().SaveTo(_settings);
        _settings.Save();
        _onSaved();
        Close();
    }

    /// <summary>While a field has the keyboard the global hotkeys are let go, so pressing one
    /// binds it instead of running it; moving to another field keeps them let go.</summary>
    private void WatchFocus(TextBox box)
    {
        _boxes.Add(box);
        box.GotKeyboardFocus += (_, _) => _holdHotkeys(true);
        box.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is TextBox next && _boxes.Contains(next)) return;
            _holdHotkeys(false);
        };
    }
}
