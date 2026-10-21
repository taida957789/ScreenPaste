using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A focused pin's key table (Snipaste's pin keys by default, every one rebindable): 1-6
/// turn, flip and recolour it, + and - zoom (with Ctrl: opacity), the arrows move it, W/A/S/D
/// move the cursor, and the outputs follow the settings. + and - go by the character the key
/// types on the current layout, since where they sit differs between layouts. Esc and Ctrl+W
/// close the pin so it can be brought back; Shift+Esc destroys it.
/// </summary>
public sealed class PinKeyRouterTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    private static readonly PinKeyRouter Router = new(ShortcutMap.Defaults);

    private static PinKeyContext Press(Key key, ModifierKeys modifiers = None, string? text = null) => new()
    {
        Key = key,
        Modifiers = modifiers,
        Text = text,
    };

    // ---------------------------------------------------------------- digits ---

    [Theory]
    [InlineData(Key.D1, PinCommand.RotateClockwise)]
    [InlineData(Key.D2, PinCommand.RotateCounterClockwise)]
    [InlineData(Key.D3, PinCommand.FlipHorizontal)]
    [InlineData(Key.D4, PinCommand.FlipVertical)]
    [InlineData(Key.D5, PinCommand.ToggleGrayscale)]
    [InlineData(Key.D6, PinCommand.ToggleInvert)]
    [InlineData(Key.NumPad1, PinCommand.RotateClockwise)]
    [InlineData(Key.NumPad6, PinCommand.ToggleInvert)]
    public void Digit_TurnsFlipsOrRecoloursThePin(Key key, PinCommand expected)
    {
        Assert.Equal(expected, Router.Route(Press(key)));
    }

    [Fact]
    public void Digit_WithAModifier_DoesNothing()
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.D1, Ctrl)));
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.D3, Shift)));
    }

    [Fact]
    public void Digit_IsMatchedByKey_EvenWhereItTypesAnotherCharacter()
    {
        // AZERTY: the 6 key types "-" unshifted; it still inverts.
        Assert.Equal(PinCommand.ToggleInvert, Router.Route(Press(Key.D6, None, "-")));
    }

    [Theory]
    [InlineData(Key.D7)]
    [InlineData(Key.D0)]
    [InlineData(Key.Q)]
    [InlineData(Key.F4)]
    public void OtherKeys_DoNothing(Key key)
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(key)));
    }

    // ---------------------------------------------------------------- zoom & opacity ---

    [Theory]
    [InlineData(Key.OemPlus, None, "=", PinCommand.ZoomIn)]
    [InlineData(Key.OemPlus, Shift, "=", PinCommand.ZoomIn)]
    [InlineData(Key.OemPlus, None, "+", PinCommand.ZoomIn)]
    [InlineData(Key.OemMinus, None, "-", PinCommand.ZoomOut)]
    [InlineData(Key.OemPlus, Ctrl, "=", PinCommand.OpacityUp)]
    [InlineData(Key.OemPlus, Ctrl | Shift, "+", PinCommand.OpacityUp)]
    [InlineData(Key.OemMinus, Ctrl, "-", PinCommand.OpacityDown)]
    public void PlusAndMinus_ByTheirCharacter_ZoomOrWithCtrlChangeTheOpacity(
        Key key, ModifierKeys modifiers, string text, PinCommand expected)
    {
        Assert.Equal(expected, Router.Route(Press(key, modifiers, text)));
    }

    [Fact]
    public void PlusAndMinus_WhereverTheLayoutPutsThem_GoByTheCharacter()
    {
        // A layout may put + and - on any OEM key: the character decides, not the key.
        Assert.Equal(PinCommand.ZoomIn, Router.Route(Press(Key.Oem6, None, "+")));
        Assert.Equal(PinCommand.ZoomOut, Router.Route(Press(Key.OemQuestion, None, "-")));
    }

    [Theory]
    [InlineData(Key.Add, None, PinCommand.ZoomIn)]
    [InlineData(Key.Subtract, None, PinCommand.ZoomOut)]
    [InlineData(Key.Add, Ctrl, PinCommand.OpacityUp)]
    [InlineData(Key.Subtract, Ctrl, PinCommand.OpacityDown)]
    public void NumpadPlusAndMinus_WorkWithoutACharacter(Key key, ModifierKeys modifiers, PinCommand expected)
    {
        Assert.Equal(expected, Router.Route(Press(key, modifiers)));
    }

    [Fact]
    public void PlusWithAlt_DoesNothing()
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.OemPlus, Alt, "=")));
    }

    [Fact]
    public void AGestureBoundToCtrlPlus_WinsOverTheOpacity()
    {
        var router = new PinKeyRouter(ShortcutMap.Defaults.WithKeys(PinShortcuts.Copy, "Ctrl+OemPlus"));

        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.OemPlus, Ctrl, "=")));
    }

    // ---------------------------------------------------------------- outputs ---

    [Theory]
    [InlineData(Key.C, Ctrl, PinCommand.Copy)]
    [InlineData(Key.S, Ctrl, PinCommand.Save)]
    [InlineData(Key.S, Ctrl | Shift, PinCommand.QuickSave)]
    [InlineData(Key.P, Ctrl, PinCommand.Print)]
    public void DefaultGestures_CopySaveQuickSaveAndPrint(Key key, ModifierKeys modifiers, PinCommand expected)
    {
        Assert.Equal(expected, Router.Route(Press(key, modifiers)));
    }

    [Fact]
    public void CopyGesture_FollowsTheSettings()
    {
        // Arrange
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "Ctrl+Shift+K" }));

        // Act / Assert
        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.K, Ctrl | Shift)));
        Assert.Equal(PinCommand.None, router.Route(Press(Key.C, Ctrl)));
    }

    [Fact]
    public void UnboundCopyGesture_LeavesCtrlInsertCopying()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "" }));

        Assert.Equal(PinCommand.None, router.Route(Press(Key.C, Ctrl)));
        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.Insert, Ctrl)));
    }

    [Fact]
    public void QuickSave_WinsWhenBothSaveGesturesAreTheSame()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { QuickSaveHotkey = "Ctrl+S" }));

        Assert.Equal(PinCommand.QuickSave, router.Route(Press(Key.S, Ctrl)));
    }

    [Fact]
    public void BareLetterInTheSettings_StaysUnbound()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { SaveHotkey = "S" }));

        Assert.Equal(PinCommand.NudgeCursor, router.Route(Press(Key.S)));
    }

    [Theory]
    [InlineData(Key.Insert, Ctrl, PinCommand.Copy)]
    [InlineData(Key.C, Ctrl | Alt, PinCommand.CopyAsFile)]
    [InlineData(Key.P, Ctrl | Shift, PinCommand.OpenSettings)]
    [InlineData(Key.Apps, None, PinCommand.OpenMenu)]
    public void FixedChords(Key key, ModifierKeys modifiers, PinCommand expected)
    {
        Assert.Equal(expected, Router.Route(Press(key, modifiers)));
    }

    // ---------------------------------------------------------------- moving ---

    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void Arrows_MoveThePin(Key key)
    {
        Assert.Equal(PinCommand.Move, Router.Route(Press(key)));
    }

    [Theory]
    [InlineData(Key.W)]
    [InlineData(Key.A)]
    [InlineData(Key.S)]
    [InlineData(Key.D)]
    public void Wasd_MovesTheCursor(Key key)
    {
        Assert.Equal(PinCommand.NudgeCursor, Router.Route(Press(key)));
    }

    [Fact]
    public void ArrowsAndWasd_WithAModifier_DoNothing()
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.Left, Shift)));
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.W, Alt)));
    }

    // ---------------------------------------------------------------- close ---

    [Fact]
    public void Esc_ClosesThePin()
    {
        Assert.Equal(PinCommand.Close, Router.Route(Press(Key.Escape)));
    }

    [Fact]
    public void ShiftEsc_DestroysThePin()
    {
        Assert.Equal(PinCommand.Destroy, Router.Route(Press(Key.Escape, Shift)));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(Alt)]
    [InlineData(Ctrl | Shift)]
    public void Esc_WithAnotherModifier_StillOnlyCloses(ModifierKeys modifiers)
    {
        Assert.Equal(PinCommand.Close, Router.Route(Press(Key.Escape, modifiers)));
    }

    [Fact]
    public void CtrlW_ClosesThePin()
    {
        Assert.Equal(PinCommand.Close, Router.Route(Press(Key.W, Ctrl)));
    }

    [Fact]
    public void CtrlShiftW_DoesNothing()
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.W, Ctrl | Shift)));
    }

    [Fact]
    public void AGestureBoundToCtrlW_WinsOverClosing()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "Ctrl+W" }));

        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.W, Ctrl)));
    }

    [Fact]
    public void AGestureBoundToEsc_DoesNotTakeOverClosing()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "Shift+Esc" }));

        Assert.Equal(PinCommand.Destroy, router.Route(Press(Key.Escape, Shift)));
    }

    // ---------------------------------------------------------------- auto-repeat ---

    [Theory]
    [InlineData(PinCommand.ZoomIn)]
    [InlineData(PinCommand.ZoomOut)]
    [InlineData(PinCommand.OpacityUp)]
    [InlineData(PinCommand.OpacityDown)]
    [InlineData(PinCommand.Move)]
    [InlineData(PinCommand.NudgeCursor)]
    public void HeldKeys_KeepZoomingFadingAndMoving(PinCommand command)
    {
        Assert.True(PinKeyRouter.RunsOnRepeat(command));
    }

    [Theory]
    [InlineData(PinCommand.RotateClockwise)]
    [InlineData(PinCommand.FlipHorizontal)]
    [InlineData(PinCommand.ToggleGrayscale)]
    [InlineData(PinCommand.ToggleInvert)]
    [InlineData(PinCommand.Copy)]
    [InlineData(PinCommand.Save)]
    [InlineData(PinCommand.Print)]
    [InlineData(PinCommand.OpenSettings)]
    [InlineData(PinCommand.OpenMenu)]
    [InlineData(PinCommand.Close)]
    [InlineData(PinCommand.Destroy)]
    public void HeldKeys_RunOneShotCommandsOnce(PinCommand command)
    {
        Assert.False(PinKeyRouter.RunsOnRepeat(command));
    }

    // ---------------------------------------------------------------- clipboard ---

    [Fact]
    public void CtrlV_ReplacesThePinWithTheClipboard()
    {
        Assert.Equal(PinCommand.PasteClipboard, Router.Route(Press(Key.V, Ctrl)));
    }

    [Fact]
    public void CtrlShiftC_CopiesTheTextThePinWasMadeFrom()
    {
        Assert.Equal(PinCommand.CopyText, Router.Route(Press(Key.C, Ctrl | Shift)));
    }

    [Fact]
    public void CtrlC_StillFollowsTheCopyGesture()
    {
        Assert.Equal(PinCommand.Copy, Router.Route(Press(Key.C, Ctrl)));
    }

    [Theory]
    [InlineData(PinCommand.PasteClipboard)]
    [InlineData(PinCommand.CopyText)]
    public void ClipboardCommands_RunOncePerPress(PinCommand command)
    {
        Assert.False(PinKeyRouter.RunsOnRepeat(command));
    }

    // ---------------------------------------------------------------- multi-selection ---

    [Fact]
    public void CtrlA_SelectsEveryShownPin()
    {
        Assert.Equal(PinCommand.SelectAll, Router.Route(Press(Key.A, Ctrl)));
    }

    [Fact]
    public void AGestureBoundToCtrlA_WinsOverSelectingAll()
    {
        var router = new PinKeyRouter(ShortcutMap.FromSettings(new AppSettings { CopyHotkey = "Ctrl+A" }));

        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.A, Ctrl)));
    }

    [Fact]
    public void BareA_StillMovesTheCursor()
    {
        Assert.Equal(PinCommand.NudgeCursor, Router.Route(Press(Key.A)));
    }

    // ---------------------------------------------------------------- loupe ---

    private static PinKeyContext WithLoupe(Key key, ModifierKeys modifiers = None) =>
        Press(key, modifiers) with { LoupeVisible = true };

    [Fact]
    public void C_WithTheLoupeShowing_CopiesTheColour()
    {
        Assert.Equal(PinCommand.CopyLoupeColor, Router.Route(WithLoupe(Key.C)));
    }

    [Fact]
    public void C_WithoutTheLoupe_DoesNothing()
    {
        Assert.Equal(PinCommand.None, Router.Route(Press(Key.C)));
    }

    [Fact]
    public void CtrlC_WithTheLoupeShowing_StillCopiesThePin()
    {
        Assert.Equal(PinCommand.Copy, Router.Route(WithLoupe(Key.C, Ctrl)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AltTap_TurnsTheLoupeOnAndOff(bool loupeVisible)
    {
        var ctx = Press(Key.LeftAlt) with { LoupeVisible = loupeVisible };

        Assert.Equal(PinCommand.ToggleLoupe, Router.RouteTap(Alt, ctx));
    }

    [Fact]
    public void ShiftTap_WithTheLoupeShowing_CyclesTheColourFormat()
    {
        Assert.Equal(PinCommand.CycleLoupeFormat, Router.RouteTap(Shift, WithLoupe(Key.LeftShift)));
    }

    [Fact]
    public void ShiftTap_WithoutTheLoupe_DoesNothing()
    {
        Assert.Equal(PinCommand.None, Router.RouteTap(Shift, Press(Key.LeftShift)));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(ModifierKeys.Windows)]
    public void OtherTaps_DoNothing(ModifierKeys tapped)
    {
        Assert.Equal(PinCommand.None, Router.RouteTap(tapped, WithLoupe(Key.LeftCtrl)));
    }

    [Theory]
    [InlineData(PinCommand.SelectAll)]
    [InlineData(PinCommand.CopyLoupeColor)]
    [InlineData(PinCommand.ToggleLoupe)]
    [InlineData(PinCommand.CycleLoupeFormat)]
    public void SelectionAndLoupeCommands_RunOncePerPress(PinCommand command)
    {
        Assert.False(PinKeyRouter.RunsOnRepeat(command));
    }

    // ---------------------------------------------------------------- rebinding ---

    private static PinRoute Run(ShortcutKey key)
    {
        var ctx = new PinKeyContext { LoupeVisible = true };
        return key.Kind switch
        {
            ShortcutKeyKind.Chord => Router.Resolve(ctx with { Key = key.Key, Modifiers = key.Modifiers }),
            ShortcutKeyKind.Character when key.Modifiers == None => Router.ResolveText(key.Character.ToString(), ctx),
            ShortcutKeyKind.Character => Router.Resolve(ctx with { Key = Key.OemPlus, Modifiers = key.Modifiers, Text = key.Character.ToString() }),
            _ => Router.ResolveTap(key.Modifiers, ctx),
        };
    }

    [Fact]
    public void EveryDefaultKey_ReachesItsOwnAction()
    {
        foreach (var action in ShortcutActions.All.Where(a => a.Area == ShortcutArea.Pin))
        foreach (var key in action.DefaultKeys)
            Assert.True(Run(key).Action?.Id == action.Id, $"{key} should run {action.Id}");
    }

    [Fact]
    public void AReboundKey_RunsTheAction_AndTheOldKeyNoLonger()
    {
        var router = new PinKeyRouter(ShortcutMap.Defaults.WithKeys(PinShortcuts.RotateClockwise, "R"));

        Assert.Equal(PinCommand.RotateClockwise, router.Route(Press(Key.R)));
        Assert.Equal(PinCommand.None, router.Route(Press(Key.D1)));
    }

    [Fact]
    public void AReboundMoveKey_StillTellsItsDirection()
    {
        var router = new PinKeyRouter(ShortcutMap.Defaults.WithKeys(PinShortcuts.MoveUp, "I"));

        var route = router.Resolve(Press(Key.I));

        Assert.Equal(PinCommand.Move, route.Command);
        Assert.Equal(new KeyChord(Key.Up, None), route.Action!.Canonical);
    }

    [Fact]
    public void ATypedCharacter_ReachesAnActionBoundToIt()
    {
        Assert.Equal(PinCommand.ZoomIn, Router.RouteText("+", Press(Key.None)));
        var router = new PinKeyRouter(ShortcutMap.Defaults.WithKeys(PinShortcuts.Invert, "!"));
        Assert.Equal(PinCommand.ToggleInvert, router.RouteText("!", Press(Key.None)));
    }

    [Fact]
    public void EscClosesWithAnyModifier_OnlyWhileEscIsACloseKey()
    {
        var router = new PinKeyRouter(ShortcutMap.Defaults.WithKeys(PinShortcuts.Close, "Ctrl+W"));

        Assert.Equal(PinCommand.None, router.Route(Press(Key.Escape)));
        Assert.Equal(PinCommand.None, router.Route(Press(Key.Escape, Ctrl)));
        Assert.Equal(PinCommand.Destroy, router.Route(Press(Key.Escape, Shift)));
    }
}
