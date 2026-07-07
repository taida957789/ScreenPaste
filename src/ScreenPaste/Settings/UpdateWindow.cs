using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenPaste.Settings;

/// <summary>Prompts the user to install an available update (download installer + relaunch).</summary>
public sealed class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private readonly TextBlock _status;
    private readonly Button _update, _page, _later;

    public UpdateWindow(UpdateInfo info)
    {
        _info = info;

        Title = Loc.T("upd.title");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(20), MaxWidth = 460 };

        body.Children.Add(new TextBlock
        {
            Text = Loc.T("upd.available", info.Version.ToString(), UpdateChecker.Current.ToString()),
            Foreground = Theme.ForegroundBrush, TextWrapping = TextWrapping.Wrap,
            FontSize = 14, Margin = new Thickness(0, 0, 0, 10),
        });

        if (!string.IsNullOrWhiteSpace(info.Notes))
        {
            body.Children.Add(new ScrollViewer
            {
                MaxHeight = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = info.Notes, Foreground = Theme.ForegroundBrush, Opacity = 0.85,
                    TextWrapping = TextWrapping.Wrap, FontSize = 12,
                },
                Margin = new Thickness(0, 0, 0, 10),
            });
        }

        _status = new TextBlock
        {
            Foreground = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed,
        };
        body.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _update = Btn(Loc.T("upd.now"), OnUpdate, isDefault: true);
        _page = Btn(Loc.T("upd.openPage"), OnOpenPage);
        _later = Btn(Loc.T("upd.later"), Close, isCancel: true);
        buttons.Children.Add(_update);
        buttons.Children.Add(_page);
        buttons.Children.Add(_later);
        body.Children.Add(buttons);

        Content = body;
    }

    private async void OnUpdate()
    {
        if (string.IsNullOrEmpty(_info.SetupUrl)) { OnOpenPage(); return; }

        SetBusy(true);
        _status.Text = Loc.T("upd.downloading");
        _status.Visibility = Visibility.Visible;

        var path = await UpdateChecker.DownloadAsync(_info.SetupUrl);
        if (path == null)
        {
            _status.Text = Loc.T("upd.failed");
            SetBusy(false);
            return;
        }

        // Launch the installer and exit so files aren't locked; the installer relaunches the app.
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { OnOpenPage(); return; }
        Application.Current.Shutdown();
    }

    private void OnOpenPage()
    {
        try { Process.Start(new ProcessStartInfo(_info.PageUrl) { UseShellExecute = true }); }
        catch { /* ignore */ }
        Close();
    }

    private void SetBusy(bool busy)
    {
        _update.IsEnabled = !busy;
        _page.IsEnabled = !busy;
        _later.IsEnabled = !busy;
    }

    private static Button Btn(string text, Action onClick, bool isDefault = false, bool isCancel = false)
    {
        var b = new Button
        {
            Content = text, MinWidth = 80, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4),
            IsDefault = isDefault, IsCancel = isCancel,
            Background = Theme.ButtonBgBrush, Foreground = Theme.ForegroundBrush, BorderBrush = Theme.ButtonBorderBrush,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
