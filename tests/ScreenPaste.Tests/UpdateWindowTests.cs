using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The update window while the installer downloads, driven without showing it or going online:
/// a bar and a percentage for how far it has got, the sizes and the speed under it, and on a
/// failure a message that says so, with the buttons back to try again. WPF controls need an STA
/// thread, hence <see cref="StaRunner"/>.
/// </summary>
public sealed class UpdateWindowTests
{
    private static UpdateWindow NewWindow() =>
        new(new UpdateInfo(new Version(9, 9, 9), "v9.9.9", "https://example.invalid/setup.exe", "https://example.invalid/", ""));

    private static T Control<T>(DependencyObject root, string id) where T : DependencyObject =>
        Find<T>(root, id) ?? throw new InvalidOperationException("no control " + id);

    private static T? Find<T>(DependencyObject node, string id) where T : DependencyObject
    {
        if (node is T match && AutomationProperties.GetAutomationId(node) == id) return match;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (Find<T>(child, id) is { } found) return found;
        return null;
    }

    private static string[] Units => Loc.T("upd.units").Split('|');

    [Fact]
    public void BeforeTheDownload_NoProgressIsShown()
    {
        StaRunner.Run(() =>
        {
            var window = NewWindow();

            Assert.Equal(Visibility.Collapsed, Control<ProgressBar>(window, "UpdateProgress").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<TextBlock>(window, "UpdateDetail").Visibility);
            window.Close();
        });
    }

    [Fact]
    public void Progress_FillsTheBar_AndSaysHowMuchHasArrivedAndHowFast()
    {
        StaRunner.Run(() =>
        {
            var window = NewWindow();
            var progress = new DownloadProgress(25 << 20, 100 << 20, 2 << 20);

            window.ShowDownloading();
            window.ShowProgress(progress);

            var bar = Control<ProgressBar>(window, "UpdateProgress");
            Assert.Equal(Visibility.Visible, bar.Visibility);
            Assert.Equal(0.25, bar.Value / bar.Maximum);
            Assert.Equal(Loc.T("upd.progress", 25), Control<TextBlock>(window, "UpdateStatus").Text);
            var detail = Control<TextBlock>(window, "UpdateDetail");
            Assert.Equal(Visibility.Visible, detail.Visibility);
            Assert.Equal(UpdateDownload.Detail(progress, Units), detail.Text);
            window.Close();
        });
    }

    [Fact]
    public void Progress_SaysAHundredPercent_OnlyWhenItIsAllThere()
    {
        StaRunner.Run(() =>
        {
            var window = NewWindow();

            window.ShowProgress(new DownloadProgress(999, 1000, 0));
            Assert.Equal(Loc.T("upd.progress", 99), Control<TextBlock>(window, "UpdateStatus").Text);

            window.ShowProgress(new DownloadProgress(1000, 1000, 0));
            Assert.Equal(Loc.T("upd.progress", 100), Control<TextBlock>(window, "UpdateStatus").Text);
            window.Close();
        });
    }

    [Fact]
    public void Progress_WithoutATotal_LeavesOutTheBar_AndStillSaysHowMuchAndHowFast()
    {
        StaRunner.Run(() =>
        {
            var window = NewWindow();
            var progress = new DownloadProgress(5 << 20, null, 1 << 20);

            window.ShowProgress(progress);

            Assert.Equal(Visibility.Collapsed, Control<ProgressBar>(window, "UpdateProgress").Visibility);
            Assert.Equal(Loc.T("upd.downloading"), Control<TextBlock>(window, "UpdateStatus").Text);
            Assert.Equal(UpdateDownload.Detail(progress, Units), Control<TextBlock>(window, "UpdateDetail").Text);
            window.Close();
        });
    }

    [Fact]
    public void Downloading_HoldsTheButtons_AndAFailure_GivesThemBack()
    {
        StaRunner.Run(() =>
        {
            var window = NewWindow();
            var update = Control<Button>(window, "UpdateNow");

            window.ShowDownloading();
            window.ShowProgress(new DownloadProgress(10, 100, 5));
            Assert.False(update.IsEnabled);

            window.ShowFailed();

            Assert.True(update.IsEnabled);
            Assert.Equal(Loc.T("upd.downloadFailed"), Control<TextBlock>(window, "UpdateStatus").Text);
            Assert.Equal(Visibility.Collapsed, Control<ProgressBar>(window, "UpdateProgress").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<TextBlock>(window, "UpdateDetail").Visibility);
            window.Close();
        });
    }
}
