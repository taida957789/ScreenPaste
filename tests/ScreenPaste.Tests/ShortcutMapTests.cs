using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Every in-app key of the capture and the pins can be changed in the settings. The table
/// (<see cref="ShortcutActions"/>) gives each action its default keys; settings.json keeps only
/// what the user changed (<see cref="AppSettings.KeyBindings"/>); the old single-gesture
/// settings (CopyHotkey ...) still count for files written before, and are kept in step.
/// </summary>
public sealed class ShortcutMapTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;

    // ---- the table ----

    [Fact]
    public void EveryActionId_IsUnique()
    {
        var ids = ShortcutActions.All.Select(a => a.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryAction_HasALabelAGroupAndAContext()
    {
        Assert.All(ShortcutActions.All, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.LabelKey));
            Assert.False(string.IsNullOrWhiteSpace(a.Group));
            Assert.NotEqual(ShortcutContexts.None, a.Contexts);
        });
    }

    [Fact]
    public void EveryDefaultKey_ParsesAndRoundTrips()
    {
        Assert.All(ShortcutActions.All.SelectMany(a => a.Defaults), text =>
            Assert.Equal(text, ShortcutKey.Parse(text)!.Value.ToString()));
    }

    [Fact]
    public void TheDefaults_HaveNoClash()
    {
        Assert.Empty(ShortcutConflicts.InApp(ShortcutMap.Defaults));
    }

    [Fact]
    public void EveryToolOfTheToolbar_HasAnAction()
    {
        var tools = ShortcutActions.All.Where(a => a.Tool != null).Select(a => a.Tool!.Value).ToHashSet();

        Assert.Equal(ToolShortcuts.SnipasteLayout.Where(t => t != ToolKind.None).ToHashSet(), tools);
    }

    [Theory]
    [InlineData(CaptureShortcuts.ToolShape, ToolKind.Shape, "Ctrl+1")]
    [InlineData(CaptureShortcuts.ToolLine, ToolKind.Line, "Ctrl+2")]
    [InlineData(CaptureShortcuts.ToolMarker, ToolKind.MarkerPen, "B", "Ctrl+3")]
    [InlineData(CaptureShortcuts.ToolHighlighter, ToolKind.Highlighter, "Ctrl+4")]
    [InlineData(CaptureShortcuts.ToolBlur, ToolKind.Blur, "M", "Ctrl+5")]
    [InlineData(CaptureShortcuts.ToolText, ToolKind.Text, "T", "Ctrl+6")]
    [InlineData(CaptureShortcuts.ToolNumber, ToolKind.Number, "Ctrl+7")]
    [InlineData(CaptureShortcuts.ToolMagnify, ToolKind.Magnify, "Ctrl+8")]
    [InlineData(CaptureShortcuts.ToolEraser, ToolKind.Eraser, "E", "Ctrl+9")]
    [InlineData(CaptureShortcuts.ToolSticker, ToolKind.Sticker, "Ctrl+0")]
    public void TheToolKeys_FollowSnipastesMeaning(string id, ToolKind tool, params string[] keys)
    {
        var action = ShortcutActions.ById(id)!;

        Assert.Equal(tool, action.Tool);
        Assert.Equal(keys, action.Defaults);
        Assert.Equal(id, CaptureShortcuts.ForTool(tool));
    }

    [Theory]
    [InlineData(CaptureShortcuts.MoveUp, Key.Up, None)]
    [InlineData(CaptureShortcuts.GrowLeft, Key.Left, Ctrl)]
    [InlineData(CaptureShortcuts.ShrinkDown, Key.Down, ModifierKeys.Shift)]
    [InlineData(CaptureShortcuts.NudgeRight, Key.D, None)]
    [InlineData(PinShortcuts.MoveDown, Key.Down, None)]
    [InlineData(PinShortcuts.NudgeUp, Key.W, None)]
    public void ADirectionalAction_TellsItsDirectionByItsDefaultKey(string id, Key key, ModifierKeys modifiers)
    {
        Assert.Equal(new KeyChord(key, modifiers), ShortcutActions.ById(id)!.Canonical);
    }

    // ---- reading the settings ----

    [Fact]
    public void ASettingsFileWithNothingChanged_GivesTheDefaults()
    {
        var map = ShortcutMap.FromSettings(new AppSettings());

        Assert.All(ShortcutActions.All, a => Assert.Equal(ShortcutMap.Defaults.For(a.Id), map.For(a.Id)));
        Assert.Equal(new[] { ShortcutKey.Chord(Key.C, Ctrl), ShortcutKey.Chord(Key.Insert, Ctrl) }, map.For(CaptureShortcuts.Copy));
    }

    [Fact]
    public void AChangedAction_ReadsItsKeys()
    {
        var settings = new AppSettings();
        settings.KeyBindings[CaptureShortcuts.PreviousRegion] = new() { "Q", "Ctrl+Alt+R" };

        var keys = ShortcutMap.FromSettings(settings).For(CaptureShortcuts.PreviousRegion);

        Assert.Equal(new[] { ShortcutKey.Chord(Key.Q, None), ShortcutKey.Chord(Key.R, Ctrl | ModifierKeys.Alt) }, keys);
    }

    [Fact]
    public void AnEmptyList_UnbindsTheAction()
    {
        var settings = new AppSettings();
        settings.KeyBindings[PinShortcuts.RotateClockwise] = new();

        Assert.Empty(ShortcutMap.FromSettings(settings).For(PinShortcuts.RotateClockwise));
    }

    [Fact]
    public void AnUnreadableKey_IsSkipped_AndATwiceListedOneKeptOnce()
    {
        var settings = new AppSettings();
        settings.KeyBindings[CaptureShortcuts.Refresh] = new() { "Ctrl+Nope", "F6", "F6" };

        Assert.Equal(new[] { ShortcutKey.Chord(Key.F6, None) }, ShortcutMap.FromSettings(settings).For(CaptureShortcuts.Refresh));
    }

    [Fact]
    public void AnUnknownActionInTheFile_IsIgnored()
    {
        var settings = new AppSettings();
        settings.KeyBindings["capture.noSuchThing"] = new() { "F9" };

        Assert.Empty(ShortcutMap.FromSettings(settings).For("capture.noSuchThing"));
    }

    [Fact]
    public void AnOldCopyGesture_ReplacesTheFirstDefault_InTheCaptureAndOnPins()
    {
        var map = ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "Ctrl+Alt+K" });

        var expected = new[] { ShortcutKey.Chord(Key.K, Ctrl | ModifierKeys.Alt), ShortcutKey.Chord(Key.Insert, Ctrl) };
        Assert.Equal(expected, map.For(CaptureShortcuts.Copy));
        Assert.Equal(expected, map.For(PinShortcuts.Copy));
    }

    [Fact]
    public void AClearedOldGesture_KeepsTheOtherDefaultKeys()
    {
        var map = ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "" });

        Assert.Equal(new[] { ShortcutKey.Chord(Key.Insert, Ctrl) }, map.For(CaptureShortcuts.Copy));
    }

    [Fact]
    public void AnOldGestureOfABareLetter_StaysUnbound_AsItAlwaysDid()
    {
        var map = ShortcutMap.FromSettings(new AppSettings { UndoHotkey = "Z" });

        Assert.Empty(map.For(CaptureShortcuts.Undo));
    }

    [Fact]
    public void TheNewList_WinsOverTheOldGesture()
    {
        var settings = new AppSettings { UndoHotkey = "Ctrl+Alt+Z" };
        settings.KeyBindings[CaptureShortcuts.Undo] = new() { "Ctrl+U" };

        Assert.Equal(new[] { ShortcutKey.Chord(Key.U, Ctrl) }, ShortcutMap.FromSettings(settings).For(CaptureShortcuts.Undo));
    }

    // ---- writing the settings ----

    [Fact]
    public void Save_WritesOnlyTheActionsThatDifferFromTheirDefaults()
    {
        var settings = new AppSettings();
        var map = ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Refresh, "F6");

        map.SaveTo(settings);

        Assert.Equal(new[] { CaptureShortcuts.Refresh }, settings.KeyBindings.Keys);
        Assert.Equal(new[] { "F6" }, settings.KeyBindings[CaptureShortcuts.Refresh]);
    }

    [Fact]
    public void Save_KeepsTheOldGestureInStep_WithTheCapturesFirstChord()
    {
        var settings = new AppSettings();

        ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Undo, "[", "Ctrl+U").SaveTo(settings);

        Assert.Equal("Ctrl+U", settings.UndoHotkey);   // the recording editor still reads it
    }

    [Fact]
    public void Save_ClearsTheOldGesture_WhenTheActionIsUnbound()
    {
        var settings = new AppSettings();

        ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Pin).SaveTo(settings);

        Assert.Equal("", settings.PinHotkey);
        Assert.Empty(ShortcutMap.FromSettings(settings).For(CaptureShortcuts.Pin));
    }

    [Fact]
    public void Save_ThenRead_GivesTheSameKeys()
    {
        var settings = new AppSettings();
        var map = ShortcutMap.Defaults
            .WithKeys(CaptureShortcuts.Copy, "Ctrl+K")
            .WithKeys(PinShortcuts.ZoomIn, "Ctrl+Up", "]")
            .WithKeys(CaptureShortcuts.ColorFormat, "Alt");

        map.SaveTo(settings);
        var back = ShortcutMap.FromSettings(settings);

        Assert.All(ShortcutActions.All, a => Assert.Equal(map.For(a.Id), back.For(a.Id)));
    }

    [Fact]
    public void Save_ChangingOnlyTheCapturesCopy_LeavesThePinsCopyAlone()
    {
        // The capture's Copy writes the old CopyHotkey, which a pin falls back on.
        var settings = new AppSettings();

        ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Copy, "Ctrl+K").SaveTo(settings);

        Assert.Equal(ShortcutMap.Defaults.For(PinShortcuts.Copy), ShortcutMap.FromSettings(settings).For(PinShortcuts.Copy));
    }

    [Fact]
    public void Save_KeepsTheListsOfActionsThisVersionDoesNotKnow()
    {
        // Written by a newer version: an older one must not drop them.
        var settings = new AppSettings();
        settings.KeyBindings["capture.fromTheFuture"] = new() { "F9" };

        ShortcutMap.FromSettings(settings).WithKeys(CaptureShortcuts.Refresh, "F6").SaveTo(settings);

        Assert.Equal(new[] { "F9" }, settings.KeyBindings["capture.fromTheFuture"]);
        Assert.Equal(new[] { "F6" }, settings.KeyBindings[CaptureShortcuts.Refresh]);
    }

    [Fact]
    public void Save_ForgetsAChangeTakenBackToTheDefault()
    {
        var settings = new AppSettings();
        ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Refresh, "F6").SaveTo(settings);

        ShortcutMap.Defaults.SaveTo(settings);

        Assert.Empty(settings.KeyBindings);
    }

    // ---- matching and labels ----

    [Fact]
    public void Matches_ChecksEveryKeyOfTheAction()
    {
        var map = ShortcutMap.Defaults;

        Assert.True(map.Matches(CaptureShortcuts.Copy, KeyInput.Press(Key.C, Ctrl)));
        Assert.True(map.Matches(CaptureShortcuts.Copy, KeyInput.Press(Key.Insert, Ctrl)));
        Assert.False(map.Matches(CaptureShortcuts.Copy, KeyInput.Press(Key.V, Ctrl)));
    }

    [Fact]
    public void Label_JoinsTheKeys_AndIsEmptyForAnUnboundAction()
    {
        var map = ShortcutMap.Defaults.WithKeys(CaptureShortcuts.Print);

        Assert.Equal("Ctrl+C / Ctrl+Insert", map.Label(CaptureShortcuts.Copy));
        Assert.Equal("", map.Label(CaptureShortcuts.Print));
    }
}
