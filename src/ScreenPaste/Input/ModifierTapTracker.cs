using System.Windows.Input;

namespace ScreenPaste.Input;

/// <summary>
/// Detects a modifier "tap": Shift (or Alt, Ctrl, Win) pressed and released with no other
/// key, mouse button or wheel event in between. A plain key-down handler cannot tell a tap
/// from the start of Shift+R or a Shift+drag, which is why the loupe's Shift colour toggle
/// used to flip whenever a Shift chord was used.
///
/// Feed it the effective key (<see cref="KeyInputNormalizer"/>) and Keyboard.Modifiers as
/// they are after the event; the window reports mouse activity through <see cref="Cancel"/>.
/// </summary>
public sealed class ModifierTapTracker
{
    private ModifierKeys _armed = ModifierKeys.None;

    /// <param name="key">The effective key that went down.</param>
    /// <param name="modifiers">Keyboard.Modifiers including this press.</param>
    /// <param name="mouseButtonDown">A mouse button is held (a drag is in progress).</param>
    public void KeyDown(Key key, ModifierKeys modifiers, bool mouseButtonDown = false)
    {
        var modifier = ModifierOf(key);
        if (modifier != ModifierKeys.None && modifier == _armed) return;   // auto-repeat while held

        // Arm only when this modifier is the only one held, so Shift pressed under an
        // already-held Ctrl (even one this tracker never saw go down) is no tap.
        _armed = modifier != ModifierKeys.None && modifiers == modifier && !mouseButtonDown
            ? modifier
            : ModifierKeys.None;
    }

    /// <summary>Returns the modifier that was tapped, or None. Any other key-up in between
    /// ends the tap.</summary>
    public ModifierKeys KeyUp(Key key)
    {
        var modifier = ModifierOf(key);
        var tapped = modifier != ModifierKeys.None && modifier == _armed ? modifier : ModifierKeys.None;
        _armed = ModifierKeys.None;
        return tapped;
    }

    /// <summary>A mouse button, the wheel or a focus change came in between: no tap.</summary>
    public void Cancel() => _armed = ModifierKeys.None;

    private static ModifierKeys ModifierOf(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None,
    };
}
