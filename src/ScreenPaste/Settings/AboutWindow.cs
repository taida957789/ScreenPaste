using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace ScreenPaste.Settings;

/// <summary>Small "About" dialog: name, version, description, and links.</summary>
public sealed class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/taida957789/ScreenPaste";
    private const string KofiUrl = "https://ko-fi.com/M6T122R1AH";

    public AboutWindow()
    {
        Title = Loc.T("tray.about").TrimEnd('…');
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.WindowBrush;
        Foreground = Theme.ForegroundBrush;

        var body = new StackPanel { Margin = new Thickness(20), MinWidth = 300 };

        body.Children.Add(new TextBlock
        {
            Text = "ScreenPaste",
            FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Theme.Accent),
        });
        body.Children.Add(new TextBlock
        {
            Text = $"{Loc.T("about.version")} {Version}",
            Foreground = Theme.ForegroundBrush, Opacity = 0.8, Margin = new Thickness(0, 2, 0, 10),
        });
        body.Children.Add(new TextBlock
        {
            Text = Loc.T("about.tagline"),
            Foreground = Theme.ForegroundBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });

        body.Children.Add(LinkLine(Loc.T("about.repo"), RepoUrl));
        body.Children.Add(LinkLine(Loc.T("about.support"), KofiUrl));

        var ok = new Button
        {
            Content = Loc.T("common.ok"), MinWidth = 76, Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, IsCancel = true,
            Background = Theme.ButtonBgBrush, Foreground = Theme.ForegroundBrush, BorderBrush = Theme.ButtonBorderBrush,
        };
        ok.Click += (_, _) => Close();
        body.Children.Add(ok);

        Content = body;
    }

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private FrameworkElement LinkLine(string label, string url)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        panel.Children.Add(new TextBlock
        {
            Text = label + "：", Foreground = Theme.ForegroundBrush, VerticalAlignment = VerticalAlignment.Center,
        });
        var link = new Hyperlink(new Run(url)) { NavigateUri = new Uri(url) };
        link.RequestNavigate += OnNavigate;
        panel.Children.Add(new TextBlock(link) { Foreground = new SolidColorBrush(Theme.Accent), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private static void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* ignore */ }
        e.Handled = true;
    }
}
