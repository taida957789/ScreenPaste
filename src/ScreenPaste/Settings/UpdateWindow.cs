using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenPaste.Settings;

/// <summary>Prompts the user to install an available update (download installer + relaunch).</summary>
public sealed class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private readonly TextBlock _status, _detail;
    private readonly ProgressBar _progress;
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
        AutomationProperties.SetAutomationId(_status, "UpdateStatus");
        body.Children.Add(_status);

        // How far the download has got, and under it the sizes and the speed.
        _progress = new ProgressBar
        {
            Height = 8, Maximum = 1, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(_progress, "UpdateProgress");
        body.Children.Add(_progress);
        _detail = new TextBlock
        {
            Foreground = Theme.ForegroundBrush, Opacity = 0.85, FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(_detail, "UpdateDetail");
        body.Children.Add(_detail);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _update = Btn(Loc.T("upd.now"), OnUpdate, isDefault: true);
        AutomationProperties.SetAutomationId(_update, "UpdateNow");
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

        ShowDownloading();
        // Progress<T> brings each report to this thread, ahead of the download finishing.
        var path = await UpdateChecker.DownloadAsync(_info.SetupUrl, new Progress<DownloadProgress>(ShowProgress));
        if (path == null) { ShowFailed(); return; }

        // Let the window draw its 100% first: starting the installer (a fresh 100 MB file,
        // which antivirus reads before it runs) can hold this thread for a moment.
        await Dispatcher.Yield(DispatcherPriority.Background);
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

    /// <summary>The download has begun: say so, and hold the buttons until it ends.</summary>
    internal void ShowDownloading()
    {
        SetBusy(true);
        _status.Text = Loc.T("upd.downloading");
        _status.Visibility = Visibility.Visible;
    }

    /// <summary>Show how far the download has got, how much has arrived and how fast.</summary>
    internal void ShowProgress(DownloadProgress progress)
    {
        var fraction = progress.Fraction;
        // The themed bar has no moving look for a size not known (it would read as full),
        // so without one only the words say how it goes.
        _progress.Value = fraction ?? 0;
        _progress.Visibility = fraction is null ? Visibility.Collapsed : Visibility.Visible;
        _status.Text = progress.Percent is { } percent ? Loc.T("upd.progress", percent) : Loc.T("upd.downloading");
        _status.Visibility = Visibility.Visible;
        _detail.Text = UpdateDownload.Detail(progress, Loc.T("upd.units").Split('|'));
        _detail.Visibility = Visibility.Visible;
    }

    /// <summary>The download failed: say so, and give the buttons back to try again.</summary>
    internal void ShowFailed()
    {
        _status.Text = Loc.T("upd.downloadFailed");
        _progress.Visibility = Visibility.Collapsed;
        _detail.Visibility = Visibility.Collapsed;
        SetBusy(false);
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
