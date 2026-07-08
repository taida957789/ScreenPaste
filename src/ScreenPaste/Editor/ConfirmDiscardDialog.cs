using System.Windows;
using System.Windows.Controls;
using ScreenPaste.Settings;

namespace ScreenPaste.Editor;

/// <summary>
/// "Discard this capture?" confirmation shown when Esc leaves the editor with
/// annotations present. Includes a don't-ask-again checkbox; standard dialog keys
/// apply (Enter = discard, Esc = keep editing).
/// </summary>
public sealed class ConfirmDiscardDialog : Window
{
    private readonly CheckBox _dontAsk;

    /// <summary>True when the user ticked "don't ask again" (persist regardless of choice).</summary>
    public bool DontAskAgain => _dontAsk.IsChecked == true;

    /// <param name="title">Optional title override (defaults to the capture wording).</param>
    /// <param name="message">Optional message override.</param>
    public ConfirmDiscardDialog(string? title = null, string? message = null)
    {
        Title = title ?? Loc.T("dlg.discardTitle");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;                     // must rise above the topmost capture overlay
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16), MaxWidth = 400 };
        body.Children.Add(new TextBlock
        {
            Text = message ?? Loc.T("dlg.discardMsg"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.ForegroundBrush,
            FontSize = 13,
        });

        _dontAsk = new CheckBox
        {
            Content = Loc.T("dlg.dontAsk"),
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Theme.ForegroundBrush,
        };
        body.Children.Add(_dontAsk);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        var discard = MakeButton(Loc.T("dlg.discardYes"));
        discard.IsDefault = true;
        discard.Click += (_, _) => DialogResult = true;
        var keep = MakeButton(Loc.T("dlg.discardNo"));
        keep.IsCancel = true;               // Esc inside the dialog keeps editing
        buttons.Children.Add(discard);
        buttons.Children.Add(keep);
        body.Children.Add(buttons);

        Content = body;
    }

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        MinWidth = 92,
        Margin = new Thickness(8, 0, 0, 0),
        Padding = new Thickness(12, 5, 12, 5),
        Background = Theme.ButtonBgBrush,
        Foreground = Theme.ForegroundBrush,
        BorderBrush = Theme.ButtonBorderBrush,
    };
}
