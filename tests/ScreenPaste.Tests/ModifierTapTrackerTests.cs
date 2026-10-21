using System.Windows.Input;
using ScreenPaste.Input;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A modifier "tap" is a press and release with nothing else in between. It drives the
/// loupe's Shift colour-format toggle, so Shift chords and Shift+drag must not count.
/// Callers pass the effective key and Keyboard.Modifiers as they are after the event.
/// </summary>
public sealed class ModifierTapTrackerTests
{
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Ctrl = ModifierKeys.Control;

    [Fact]
    public void ShiftDownThenUp_IsAShiftTap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);
        var tapped = taps.KeyUp(Key.LeftShift);

        Assert.Equal(Shift, tapped);
    }

    [Fact]
    public void RightShift_CountsAsAShiftTapToo()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.RightShift, Shift);

        Assert.Equal(Shift, taps.KeyUp(Key.RightShift));
    }

    [Fact]
    public void AutoRepeatWhileHeld_StillEndsInATap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);
        taps.KeyDown(Key.LeftShift, Shift);
        taps.KeyDown(Key.LeftShift, Shift);

        Assert.Equal(Shift, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void ShiftR_IsNotATap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);
        taps.KeyDown(Key.R, Shift);
        taps.KeyUp(Key.R);
        var tapped = taps.KeyUp(Key.LeftShift);

        Assert.Equal(ModifierKeys.None, tapped);
    }

    [Fact]
    public void AnotherKeyReleasedWhileShiftIsHeld_CancelsTheTap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);   // R was already held before Shift went down
        taps.KeyUp(Key.R);

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void AMouseButtonOrWheelInBetween_CancelsTheTap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);
        taps.Cancel();   // the window reports a mouse button or wheel event

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void ShiftPressedDuringADrag_IsNotATap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift, mouseButtonDown: true);   // Shift+drag snapping

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void AltC_IsNotAnAltTap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftAlt, Alt);
        taps.KeyDown(Key.C, Alt);
        taps.KeyUp(Key.C);
        var tapped = taps.KeyUp(Key.LeftAlt);

        Assert.Equal(ModifierKeys.None, tapped);
    }

    [Fact]
    public void AltDownThenUp_IsAnAltTap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.RightAlt, Alt);

        Assert.Equal(Alt, taps.KeyUp(Key.RightAlt));
    }

    [Fact]
    public void ShiftPressedWhileCtrlIsHeld_IsNotATap()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftCtrl, Ctrl);
        taps.KeyDown(Key.LeftShift, Ctrl | Shift);
        var shiftTap = taps.KeyUp(Key.LeftShift);
        var ctrlTap = taps.KeyUp(Key.LeftCtrl);

        Assert.Equal(ModifierKeys.None, shiftTap);
        Assert.Equal(ModifierKeys.None, ctrlTap);
    }

    [Fact]
    public void ShiftPressedWhileAnUnseenCtrlIsHeld_IsNotATap()
    {
        var taps = new ModifierTapTracker();

        // Ctrl went down before the overlay had focus, so only the modifier state shows it.
        taps.KeyDown(Key.LeftShift, Ctrl | Shift);

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void ATapFiresOnce()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.LeftShift, Shift);
        taps.KeyUp(Key.LeftShift);

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.LeftShift));
    }

    [Fact]
    public void AnOrdinaryKey_NeverTaps()
    {
        var taps = new ModifierTapTracker();

        taps.KeyDown(Key.C, ModifierKeys.None);

        Assert.Equal(ModifierKeys.None, taps.KeyUp(Key.C));
    }

    [Fact]
    public void ACancelledTap_DoesNotBlockTheNextOne()
    {
        var taps = new ModifierTapTracker();
        taps.KeyDown(Key.LeftShift, Shift);
        taps.Cancel();
        taps.KeyUp(Key.LeftShift);

        taps.KeyDown(Key.LeftShift, Shift);
        var tapped = taps.KeyUp(Key.LeftShift);

        Assert.Equal(Shift, tapped);
    }
}
