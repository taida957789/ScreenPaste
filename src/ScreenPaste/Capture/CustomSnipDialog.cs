using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// Snipaste's custom snip (Shift+F1): the area to snip as left, top, width and height in
/// physical screen px, filled in with the last snip's area, and a delay in seconds. Snip hands
/// the checked request (<see cref="CustomSnipRequest.Validate"/>) to <see cref="Accepted"/>
/// and closes; App waits out the delay and opens the capture with that area chosen.
/// Built in code like the settings window.
/// </summary>
public sealed class CustomSnipDialog : Window
{
    private const double FieldWidth = 90;

    private readonly TextBox _left, _top, _width, _height, _delay;
    private readonly TextBlock _error;

    /// <summary>Raised with the checked request when the user clicks Snip.</summary>
    public event Action<CustomSnipRequest>? Accepted;

    /// <param name="initial">The area filled in (physical screen px).</param>
    /// <param name="lastArea">The last snip's area, for the "Last area" button; null disables it.</param>
    /// <param name="delaySeconds">The delay filled in.</param>
    public CustomSnipDialog(Rect initial, Rect? lastArea, double delaySeconds)
    {
        Title = Loc.T("custom.title");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;   // above the pins it may be meant to snip
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        body.Children.Add(SettingsWindow.Hint(Loc.T("custom.hint")));
        _left = Field(body, "custom.left", initial.X);
        _top = Field(body, "custom.top", initial.Y);
        _width = Field(body, "custom.width", initial.Width);
        _height = Field(body, "custom.height", initial.Height);
        _delay = Field(body, "set.snipDelay", CustomSnipRequest.ClampDelay(delaySeconds));

        _error = new TextBlock
        {
            Text = Loc.T("custom.invalid"), Foreground = SettingsWindow.WarningBrush, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, MaxWidth = 300,
        };
        body.Children.Add(_error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var last = SettingsWindow.TextButton(Loc.T("custom.lastArea"), () => { if (lastArea is { } r) Fill(r); });
        last.IsEnabled = lastArea != null;
        buttons.Children.Add(last);
        buttons.Children.Add(SettingsWindow.TextButton(Loc.T("set.capture"), Snip, isDefault: true));
        var cancel = SettingsWindow.TextButton(Loc.T("common.cancel"), Close);
        cancel.IsCancel = true;
        buttons.Children.Add(cancel);
        body.Children.Add(buttons);

        Content = body;
        Loaded += (_, _) => { _left.Focus(); _left.SelectAll(); };
    }

    private static TextBox Field(Panel body, string labelKey, double value)
    {
        var box = SettingsWindow.Themed(new TextBox
        {
            Text = value.ToString("0.##", CultureInfo.CurrentCulture),   // the delay reads back the same way
            Width = FieldWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
        });
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
        body.Children.Add(SettingsWindow.Row(Loc.T(labelKey), box));
        return box;
    }

    private void Fill(Rect area)
    {
        _left.Text = Number(area.X);
        _top.Text = Number(area.Y);
        _width.Text = Number(area.Width);
        _height.Text = Number(area.Height);
        _error.Visibility = Visibility.Collapsed;
    }

    private static string Number(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    private void Snip()
    {
        var request = ReadInt(_left) is { } x && ReadInt(_top) is { } y && ReadInt(_width) is { } w &&
                      ReadInt(_height) is { } h && ReadDouble(_delay) is { } delay
            ? CustomSnipRequest.Validate(new Int32Rect(x, y, w, h), delay, VirtualScreen.Current)
            : null;
        if (request == null)
        {
            _error.Visibility = Visibility.Visible;
            return;
        }
        Close();
        Accepted?.Invoke(request);
    }

    private static int? ReadInt(TextBox box) =>
        int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;

    private static double? ReadDouble(TextBox box)
    {
        var text = box.Text.Trim();
        if (text.Length == 0) return 0;
        // The user's own decimal separator, or the invariant one.
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)) return v;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : null;
    }
}
