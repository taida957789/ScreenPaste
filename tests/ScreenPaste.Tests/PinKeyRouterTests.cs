using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A focused pin's key table (Snipaste's pin keys): 1-6 turn, flip and recolour it, + and -
/// zoom (with Ctrl: opacity), the arrows move it, W/A/S/D move the cursor, the Copy / Save /
/// Quick save / Print gestures follow the settings, and Ctrl+Insert, Ctrl+Alt+C,
/// Ctrl+Shift+P and the Menu key are fixed. + and - go by the character the key types on the
/// current layout, since where they sit differs between layouts.
/// </summary>
public sealed class PinKeyRouterTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    private static readonly PinKeyRouter Router = new(PinKeyBindings.FromSettings(new AppSettings()));

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
        var router = new PinKeyRouter(new PinKeyBindings(KeyChord.Parse("Ctrl+OemPlus"), null, null, null));

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
        var router = new PinKeyRouter(PinKeyBindings.FromSettings(new AppSettings { CopyHotkey = "Ctrl+Shift+K" }));

        // Act / Assert
        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.K, Ctrl | Shift)));
        Assert.Equal(PinCommand.None, router.Route(Press(Key.C, Ctrl)));
    }

    [Fact]
    public void UnboundCopyGesture_LeavesCtrlInsertCopying()
    {
        var router = new PinKeyRouter(PinKeyBindings.FromSettings(new AppSettings { CopyHotkey = "" }));

        Assert.Equal(PinCommand.None, router.Route(Press(Key.C, Ctrl)));
        Assert.Equal(PinCommand.Copy, router.Route(Press(Key.Insert, Ctrl)));
    }

    [Fact]
    public void QuickSave_WinsWhenBothSaveGesturesAreTheSame()
    {
        var router = new PinKeyRouter(PinKeyBindings.FromSettings(new AppSettings { QuickSaveHotkey = "Ctrl+S" }));

        Assert.Equal(PinCommand.QuickSave, router.Route(Press(Key.S, Ctrl)));
    }

    [Fact]
    public void BareLetterInTheSettings_StaysUnbound()
    {
        var router = new PinKeyRouter(PinKeyBindings.FromSettings(new AppSettings { SaveHotkey = "S" }));

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
    public void HeldKeys_RunOneShotCommandsOnce(PinCommand command)
    {
        Assert.False(PinKeyRouter.RunsOnRepeat(command));
    }

    // ---------------------------------------------------------------- menu labels ---

    [Theory]
    [InlineData(PinCommand.Close)]
    [InlineData(PinCommand.RotateClockwise)]
    [InlineData(PinCommand.RotateCounterClockwise)]
    [InlineData(PinCommand.FlipHorizontal)]
    [InlineData(PinCommand.FlipVertical)]
    [InlineData(PinCommand.ToggleGrayscale)]
    [InlineData(PinCommand.ToggleInvert)]
    [InlineData(PinCommand.CopyAsFile)]
    [InlineData(PinCommand.OpenSettings)]
    public void FixedKeys_RouteBackToTheirCommand(PinCommand command)
    {
        var chord = KeyChord.Parse(PinKeyRouter.FixedKeys(command));

        Assert.NotNull(chord);
        Assert.Equal(command, Router.Route(Press(chord.Value.Key, chord.Value.Modifiers)));
    }

    [Theory]
    [InlineData(PinCommand.OpacityUp, Key.OemPlus, "+")]
    [InlineData(PinCommand.OpacityDown, Key.OemMinus, "-")]
    public void FixedKeys_ForOpacity_AreCtrlAndTheTypedCharacter(PinCommand command, Key key, string text)
    {
        Assert.Equal("Ctrl+" + text, PinKeyRouter.FixedKeys(command));
        Assert.Equal(command, Router.Route(Press(key, Ctrl, text)));
    }

    [Theory]
    [InlineData(PinCommand.Copy)]
    [InlineData(PinCommand.Save)]
    [InlineData(PinCommand.QuickSave)]
    [InlineData(PinCommand.Print)]
    [InlineData(PinCommand.Move)]
    [InlineData(PinCommand.None)]
    public void FixedKeys_ForAGestureFromTheSettingsOrNoMenuRow_IsNull(PinCommand command)
    {
        Assert.Null(PinKeyRouter.FixedKeys(command));
    }
}
