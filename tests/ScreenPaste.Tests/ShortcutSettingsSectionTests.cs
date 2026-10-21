using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScreenPaste.Capture;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The settings window's shortcut rows and the custom snip dialog, as far as they can be
/// driven without showing them: the rows save what they show, a gesture typed twice is
/// named under them at once, and the dialog refuses an area it cannot read. WPF controls
/// need an STA thread, hence <see cref="StaRunner"/>.
/// </summary>
public sealed class ShortcutSettingsSectionTests
{
    /// <summary>The field in the row labelled <paramref name="label"/>, anywhere under <paramref name="root"/>.</summary>
    private static TextBox FieldFor(Panel root, string label)
    {
        foreach (var child in root.Children)
        {
            if (child is Grid row && row.Children.OfType<TextBlock>().Any(t => t.Text == label))
                return row.Children.OfType<TextBox>().Single();
            if (child is Panel inner && TryFieldFor(inner, label) is { } found) return found;
        }
        throw new InvalidOperationException("no row " + label);
    }

    private static TextBox? TryFieldFor(Panel root, string label)
    {
        try { return FieldFor(root, label); }
        catch (InvalidOperationException) { return null; }
    }

    private static TextBlock Warning(Panel body) => body.Children.OfType<TextBlock>().Last();

    [Fact]
    public void TheSection_SavesWhatItShows()
    {
        StaRunner.Run(() =>
        {
            var settings = new AppSettings { ClickThroughHotkey = "Ctrl+F9", FullScreenCopyHotkey = "Ctrl+F10", SnipDelaySeconds = 5 };
            var section = new ShortcutSettingsSection(settings, _ => { });
            section.AddTo(new StackPanel());
            var saved = new AppSettings { PasteHotkey = "", SnipDelaySeconds = 0 };

            section.Save(saved);

            Assert.Equal("Ctrl+F9", saved.ClickThroughHotkey);
            Assert.Equal("Ctrl+F10", saved.FullScreenCopyHotkey);
            Assert.Equal("F3", saved.PasteHotkey);
            Assert.Equal("Ctrl+Shift+Z", saved.ClearAllHotkey);
            Assert.Equal(5, saved.SnipDelaySeconds);
        });
    }

    [Fact]
    public void AGestureTypedTwice_IsNamedUnderTheRows()
    {
        StaRunner.Run(() =>
        {
            var section = new ShortcutSettingsSection(new AppSettings(), _ => { });
            var body = new StackPanel();
            section.AddTo(body);
            Assert.Equal(Visibility.Collapsed, Warning(body).Visibility);

            ShortcutSettingsSection.SetGesture(FieldFor(body, Loc.T("set.clickThrough")), "F3");

            Assert.Equal(Visibility.Visible, Warning(body).Visibility);
            Assert.Contains(Loc.T("set.paste"), Warning(body).Text);

            ShortcutSettingsSection.SetGesture(FieldFor(body, Loc.T("set.clickThrough")), "");

            Assert.Equal(Visibility.Collapsed, Warning(body).Visibility);
        });
    }

    [Fact]
    public void APunctuationKey_ShowsItsCharacter_AndSavesItsName()
    {
        StaRunner.Run(() =>
        {
            var section = new ShortcutSettingsSection(new AppSettings { ClickThroughHotkey = "Shift+OemOpenBrackets" }, _ => { });
            var body = new StackPanel();
            section.AddTo(body);
            var saved = new AppSettings();

            section.Save(saved);

            var shown = FieldFor(body, Loc.T("set.clickThrough")).Text;
            Assert.Equal(ShortcutText.Gesture("Shift+OemOpenBrackets"), shown);
            Assert.DoesNotContain("Oem", shown);   // "Shift+[" on a US layout
            Assert.Equal("Shift+OemOpenBrackets", saved.ClickThroughHotkey);
        });
    }

    [Fact]
    public void TheExtraModes_AreFoldedAway_UnlessOneIsAssigned()
    {
        StaRunner.Run(() =>
        {
            var folded = new StackPanel();
            new ShortcutSettingsSection(new AppSettings(), _ => { }).AddTo(folded);
            var open = new StackPanel();
            new ShortcutSettingsSection(new AppSettings { DelayedSnipHotkey = "Ctrl+F8" }, _ => { }).AddTo(open);

            Assert.Contains(folded.Children.OfType<StackPanel>(), p => p.Visibility == Visibility.Collapsed);
            Assert.DoesNotContain(open.Children.OfType<StackPanel>(), p => p.Visibility == Visibility.Collapsed);
        });
    }

    [Fact]
    public void TheCustomSnipDialog_RefusesAnAreaItCannotRead()
    {
        StaRunner.Run(() =>
        {
            var dialog = new CustomSnipDialog(new Rect(10, 20, 300, 200), lastArea: null, delaySeconds: 3);
            CustomSnipRequest? accepted = null;
            dialog.Accepted += r => accepted = r;
            var body = (Panel)dialog.Content;
            FieldFor(body, Loc.T("custom.width")).Text = "wide";

            var snip = body.Children.OfType<StackPanel>().Last().Children.OfType<Button>().Single(b => b.IsDefault);
            snip.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Null(accepted);
            Assert.Contains(body.Children.OfType<TextBlock>(), t => t.Text == Loc.T("custom.invalid") && t.Visibility == Visibility.Visible);
            dialog.Close();
        });
    }

    [Fact]
    public void TheCustomSnipDialog_FillsInTheLastArea()
    {
        StaRunner.Run(() =>
        {
            var dialog = new CustomSnipDialog(new Rect(0, 0, 100, 100), lastArea: new Rect(-50, 40, 640, 480), delaySeconds: 0);
            var body = (Panel)dialog.Content;
            var last = body.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First();
            Assert.True(last.IsEnabled);

            last.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal("-50", FieldFor(body, Loc.T("custom.left")).Text);
            Assert.Equal("480", FieldFor(body, Loc.T("custom.height")).Text);
            dialog.Close();
        });
    }
}
