using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The global hotkeys as one table: each row has its own RegisterHotKey id and settings
/// property, an empty gesture leaves it unassigned (nothing is registered), and the gesture
/// goes to Win32 through HotkeyGesture.ToWin32. The settings window warns about gestures
/// used twice, among the global hotkeys and against the in-app keys (ShortcutConflicts).
/// </summary>
public sealed class GlobalHotkeyTableTests
{
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkF1 = 0x70;
    private const uint VkF3 = 0x72;

    // ---------------------------------------------------------------- the table ---

    [Fact]
    public void EveryRow_HasItsOwnId()
    {
        var ids = GlobalHotkeyTable.All.Select(h => h.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryAction_HasExactlyOneRow()
    {
        var actions = GlobalHotkeyTable.All.Select(h => h.Action).ToList();

        Assert.Equal(Enum.GetValues<GlobalHotkeyAction>().Length, actions.Count);
        Assert.Equal(actions.Count, actions.Distinct().Count());
    }

    [Fact]
    public void TheCaptureAndRecordIds_StayAsTheyWere()
    {
        Assert.Equal(0xB001, GlobalHotkeyTable.For(GlobalHotkeyAction.Capture).Id);
        Assert.Equal(0xB002, GlobalHotkeyTable.For(GlobalHotkeyAction.Record).Id);
        Assert.Equal(0xB003, GlobalHotkeyTable.For(GlobalHotkeyAction.SnipAndCopy).Id);
    }

    [Fact]
    public void ById_FindsTheRow_OrNothing()
    {
        Assert.Equal(GlobalHotkeyAction.Paste, GlobalHotkeyTable.ById(GlobalHotkeyTable.For(GlobalHotkeyAction.Paste).Id)?.Action);
        Assert.Null(GlobalHotkeyTable.ById(0x1234));
    }

    [Theory]
    [InlineData(GlobalHotkeyAction.Capture, "F1")]
    [InlineData(GlobalHotkeyAction.Record, "F2")]
    [InlineData(GlobalHotkeyAction.SnipAndCopy, "Ctrl+F1")]
    [InlineData(GlobalHotkeyAction.CustomSnip, "Shift+F1")]
    [InlineData(GlobalHotkeyAction.Paste, "F3")]
    [InlineData(GlobalHotkeyAction.HidePins, "Shift+F3")]
    [InlineData(GlobalHotkeyAction.ClickThrough, "")]
    [InlineData(GlobalHotkeyAction.SnipAndPin, "")]
    [InlineData(GlobalHotkeyAction.SnipCopyAndPin, "")]
    [InlineData(GlobalHotkeyAction.LastAreaCopy, "")]
    [InlineData(GlobalHotkeyAction.FullScreenCopy, "")]
    [InlineData(GlobalHotkeyAction.ActiveWindow, "")]
    [InlineData(GlobalHotkeyAction.ActiveScreen, "")]
    [InlineData(GlobalHotkeyAction.DelayedSnip, "")]
    [InlineData(GlobalHotkeyAction.ClearSnipHistory, "")]
    public void Defaults_AreSnipastes_AndTheExtraModesStartUnassigned(GlobalHotkeyAction action, string expected)
    {
        Assert.Equal(expected, GlobalHotkeyTable.For(action).Get(new AppSettings()));
    }

    [Fact]
    public void EveryRow_ReadsBackWhatItWrites()
    {
        var settings = new AppSettings();
        foreach (var hotkey in GlobalHotkeyTable.All)
        {
            hotkey.Set(settings, "Ctrl+Alt+F" + (hotkey.Id - 0xB000));

            Assert.Equal("Ctrl+Alt+F" + (hotkey.Id - 0xB000), hotkey.Get(settings));
        }
    }

    [Fact]
    public void TheExtraModes_GoInTheMoreSection()
    {
        Assert.False(GlobalHotkeyTable.For(GlobalHotkeyAction.Paste).IsExtra);
        Assert.False(GlobalHotkeyTable.For(GlobalHotkeyAction.ClickThrough).IsExtra);
        Assert.True(GlobalHotkeyTable.For(GlobalHotkeyAction.FullScreenCopy).IsExtra);
        Assert.True(GlobalHotkeyTable.For(GlobalHotkeyAction.ClearSnipHistory).IsExtra);
    }

    // ---------------------------------------------------------------- bindings ---

    [Fact]
    public void Bindings_SkipEveryUnassignedRow()
    {
        var bindings = GlobalHotkeyTable.Bindings(new AppSettings());

        Assert.Equal(
            new[]
            {
                GlobalHotkeyAction.Capture, GlobalHotkeyAction.Record, GlobalHotkeyAction.SnipAndCopy,
                GlobalHotkeyAction.CustomSnip, GlobalHotkeyAction.Paste, GlobalHotkeyAction.HidePins,
            },
            bindings.Select(b => b.Hotkey.Action));
    }

    [Fact]
    public void TheCaptureHotkey_IsSwitchedOnByDefault()
    {
        Assert.True(new AppSettings().CaptureHotkeyEnabled);
    }

    [Fact]
    public void Bindings_LeaveOutTheCaptureHotkey_WhileTheTraySwitchedItOff()
    {
        var settings = new AppSettings { CaptureHotkeyEnabled = false };

        var actions = GlobalHotkeyTable.Bindings(settings).Select(b => b.Hotkey.Action).ToList();

        Assert.DoesNotContain(GlobalHotkeyAction.Capture, actions);
        Assert.Contains(GlobalHotkeyAction.SnipAndCopy, actions);   // only the capture hotkey is let go
        Assert.Contains(GlobalHotkeyAction.Paste, actions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Bindings_TreatABlankGestureAsUnassigned(string gesture)
    {
        var settings = new AppSettings { PasteHotkey = gesture };

        Assert.DoesNotContain(GlobalHotkeyTable.Bindings(settings), b => b.Hotkey.Action == GlobalHotkeyAction.Paste);
    }

    [Fact]
    public void Bindings_ConvertTheGestureForRegisterHotKey()
    {
        var bindings = GlobalHotkeyTable.Bindings(new AppSettings()).ToDictionary(b => b.Hotkey.Action);

        Assert.Equal((0u, VkF3), bindings[GlobalHotkeyAction.Paste].Win32);
        Assert.Equal((ModShift, VkF3), bindings[GlobalHotkeyAction.HidePins].Win32);
        Assert.Equal((ModControl, VkF1), bindings[GlobalHotkeyAction.SnipAndCopy].Win32);
    }

    [Fact]
    public void Bindings_KeepAnInvalidGesture_WithoutWin32_SoItCanBeReported()
    {
        var settings = new AppSettings { ClickThroughHotkey = "Ctrl+Nope" };

        var binding = Assert.Single(GlobalHotkeyTable.Bindings(settings), b => b.Hotkey.Action == GlobalHotkeyAction.ClickThrough);

        Assert.Null(binding.Win32);
        Assert.Equal("Ctrl+Nope", binding.Gesture);
    }

    [Fact]
    public void Bindings_TrimTheGesture()
    {
        var settings = new AppSettings { ClickThroughHotkey = "  Ctrl+F4 " };

        var binding = Assert.Single(GlobalHotkeyTable.Bindings(settings), b => b.Hotkey.Action == GlobalHotkeyAction.ClickThrough);

        Assert.Equal("Ctrl+F4", binding.Gesture);
    }

    // ---------------------------------------------------------------- conflicts ---

    private static ShortcutEntry Global(string name, string gesture) => new(name, gesture, ShortcutScope.Global);
    private static ShortcutEntry InApp(string name, string gesture) => new(name, gesture, ShortcutScope.InApp);
    private static ShortcutEntry BuiltIn(string gesture) => new(gesture, gesture, ShortcutScope.BuiltIn);

    [Fact]
    public void TheDefaultSettings_HaveNoConflicts()
    {
        Assert.Empty(ShortcutConflicts.ForSettings(new AppSettings(), key => key));
    }

    [Fact]
    public void TwoGlobalHotkeysOnOneGesture_AreADuplicate_HoweverTheyAreSpelt()
    {
        var conflicts = ShortcutConflicts.Find(new[] { Global("Capture", "Ctrl+Shift+A"), Global("Paste", "Shift+Ctrl+A") });

        var conflict = Assert.Single(conflicts);
        Assert.Equal(ShortcutConflictKind.GlobalDuplicate, conflict.Kind);
        Assert.Equal(new KeyChord(Key.A, ModifierKeys.Control | ModifierKeys.Shift), conflict.Chord);
        Assert.Equal(new[] { "Capture", "Paste" }, conflict.Names);
    }

    [Fact]
    public void AGlobalHotkeyOnAnInAppGesture_TakesTheKeyFromTheCapture()
    {
        var conflicts = ShortcutConflicts.Find(new[] { Global("Paste", "Ctrl+Z"), InApp("Undo", "Ctrl+Z") });

        var conflict = Assert.Single(conflicts);
        Assert.Equal(ShortcutConflictKind.GlobalTakesInAppKey, conflict.Kind);
        Assert.Equal(new[] { "Paste", "Undo" }, conflict.Names);
    }

    [Fact]
    public void AGlobalHotkeyOnABuiltInKey_TakesTheKeyFromTheCapture()
    {
        var conflicts = ShortcutConflicts.Find(new[] { Global("Paste", "F5"), BuiltIn("F5") });

        var conflict = Assert.Single(conflicts);
        Assert.Equal(ShortcutConflictKind.GlobalTakesInAppKey, conflict.Kind);
        Assert.Equal(new[] { "Paste" }, conflict.Names);
    }

    [Fact]
    public void TwoInAppGesturesOnOneChord_AreADuplicate()
    {
        var conflict = Assert.Single(ShortcutConflicts.Find(new[] { InApp("Undo", "Ctrl+Q"), InApp("Copy", "Ctrl+Q") }));

        Assert.Equal(ShortcutConflictKind.InAppDuplicate, conflict.Kind);
    }

    [Fact]
    public void AnInAppGestureOnABuiltInKey_ReplacesIt()
    {
        var conflict = Assert.Single(ShortcutConflicts.Find(new[] { InApp("Copy", "Ctrl+A"), BuiltIn("Ctrl+A") }));

        Assert.Equal(ShortcutConflictKind.InAppReplacesBuiltIn, conflict.Kind);
        Assert.Equal(new[] { "Copy" }, conflict.Names);
    }

    [Fact]
    public void BuiltInKeysAlone_NeverConflict()
    {
        // Ctrl+Insert copies in the capture and on a pin alike: two contexts, one meaning.
        Assert.Empty(ShortcutConflicts.Find(new[] { BuiltIn("Ctrl+Insert"), BuiltIn("Ctrl+Insert") }));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Ctrl+Nope")]
    public void UnassignedOrUnreadableGestures_AreLeftOut(string gesture)
    {
        Assert.Empty(ShortcutConflicts.Find(new[] { Global("Capture", gesture), Global("Paste", gesture) }));
    }

    [Fact]
    public void ForSettings_SeesADuplicateGlobalHotkey()
    {
        var settings = new AppSettings { ClickThroughHotkey = "F3" };

        var conflict = Assert.Single(ShortcutConflicts.ForSettings(settings, key => key));

        Assert.Equal(ShortcutConflictKind.GlobalDuplicate, conflict.Kind);
        Assert.Equal(
            new[] { GlobalHotkeyTable.For(GlobalHotkeyAction.Paste).LabelKey, GlobalHotkeyTable.For(GlobalHotkeyAction.ClickThrough).LabelKey },
            conflict.Names);
    }

    [Fact]
    public void ForSettings_SeesAGlobalHotkeyOnTheCopyGesture()
    {
        var settings = new AppSettings { HidePinsHotkey = "Ctrl+C" };

        var conflict = Assert.Single(ShortcutConflicts.ForSettings(settings, key => key));

        Assert.Equal(ShortcutConflictKind.GlobalTakesInAppKey, conflict.Kind);
    }

    [Theory]
    [InlineData("Ctrl+A")]        // the whole screen, in the capture
    [InlineData("Ctrl+Alt+C")]    // copy as a file, in the capture and on a pin
    [InlineData("Ctrl+W")]        // close a pin
    [InlineData("Ctrl+V")]        // a pin takes the clipboard
    [InlineData("Ctrl+Shift+C")]  // copy an annotation; a text pin's text
    [InlineData("F5")]            // refresh the screenshot
    public void ForSettings_KnowsTheBuiltInKeys(string gesture)
    {
        var settings = new AppSettings { UndoHotkey = gesture };

        var conflict = Assert.Single(ShortcutConflicts.ForSettings(settings, key => key));

        Assert.Equal(ShortcutConflictKind.InAppReplacesBuiltIn, conflict.Kind);
    }

    [Fact]
    public void InAppShortcutTable_ReadsBackWhatItWrites()
    {
        var settings = new AppSettings();
        foreach (var shortcut in InAppShortcutTable.All)
        {
            shortcut.Set(settings, "Ctrl+Alt+F9");

            Assert.Equal("Ctrl+Alt+F9", shortcut.Get(settings));
        }
        Assert.Equal(8, InAppShortcutTable.All.Count);
    }
}
