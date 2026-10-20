using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>
/// Keyboard handling for a focused pin. Which command a key runs is decided by the pure
/// <see cref="PinKeyRouter"/>; this half builds its context and runs the result, as the
/// menu does for its entries; a modifier tapped on its own goes through
/// <see cref="PinKeyRouter.RouteTap"/> (Alt: the loupe). The IME is off for pins, so 1-6,
/// W/A/S/D and C arrive as keys rather than ImeProcessed while a Chinese or Japanese IME is on.
/// </summary>
public sealed partial class PinWindow
{
    private const int MaxNudgeRepeat = 64;     // CursorNudge.Step has long since capped
    private const uint DeadKeyFlag = 0x80000000;
    private int _nudgeRepeat;                  // auto-repeats of the held arrow or W/A/S/D key
    private bool _altChordRan;                 // a key held with Alt ran a command since Alt went down

    private void InitKeyboard()
    {
        InputMethod.SetIsInputMethodEnabled(this, false);
        PreviewKeyDown += Pin_PreviewKeyDown;
        PreviewKeyUp += Pin_PreviewKeyUp;
    }

    private void Pin_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = KeyInputNormalizer.Effective(e);
        _modifierTaps.KeyDown(key, Keyboard.Modifiers, AnyMouseButtonDown());
        if (IsAltKey(key) && !e.IsRepeat) _altChordRan = false;

        var ctx = new PinKeyContext
        {
            Key = key,
            Modifiers = Keyboard.Modifiers,
            Text = LayoutText(key),
            IsRepeat = e.IsRepeat,
            LoupeVisible = LoupeShowing,
        };
        // Built per press, so a gesture changed in the settings applies to open pins at once.
        var command = new PinKeyRouter(PinKeyBindings.FromSettings(_settings)).Route(ctx);
        if (command == PinCommand.None) return;

        e.Handled = true;
        if (ctx.Modifiers.HasFlag(ModifierKeys.Alt)) _altChordRan = true;
        if (ctx.IsRepeat && !PinKeyRouter.RunsOnRepeat(command)) return;   // a held key runs it once
        RunCommand(command, ctx);
    }

    /// <summary>A modifier tapped on its own runs its command (Alt: the loupe; Shift: its
    /// colour format), and the release is kept from Windows, which would take an Alt tap into
    /// menu mode. Windows never saw the handled key of an Alt chord (Ctrl+Alt+C) either, so it
    /// would take that Alt release for a tap too. And the Menu key opened the menu on its way
    /// down, where WPF would open it again on the way up.</summary>
    private void Pin_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = KeyInputNormalizer.Effective(e);
        var tapped = _modifierTaps.KeyUp(key);
        if (key == Key.Apps && ContextMenu is { IsOpen: true })
        {
            e.Handled = true;
            return;
        }
        if (tapped != ModifierKeys.None)
        {
            var ctx = new PinKeyContext { Key = key, Modifiers = Keyboard.Modifiers, LoupeVisible = LoupeShowing };
            var command = PinKeyRouter.RouteTap(tapped, ctx);
            if (command != PinCommand.None)
            {
                e.Handled = true;
                _altChordRan = false;
                RunCommand(command, ctx);
                return;
            }
        }
        if (!IsAltKey(key) || !_altChordRan) return;
        _altChordRan = false;
        e.Handled = true;
    }

    /// <summary>Run a routed command. The menu runs its entries through here as well, with
    /// a default context (no key). Closing, destroying, moves and opacity act on every pin
    /// picked with this one (Ctrl+click, Ctrl+A).</summary>
    private void RunCommand(PinCommand command, PinKeyContext ctx)
    {
        switch (command)
        {
            case PinCommand.Close: ForGroup(PinManager.CloseRecoverable); break;
            case PinCommand.Destroy: ForGroup(PinManager.Destroy); break;
            case PinCommand.RotateClockwise: Apply(_state.RotateClockwise()); break;
            case PinCommand.RotateCounterClockwise: Apply(_state.RotateCounterClockwise()); break;
            case PinCommand.FlipHorizontal: Apply(_state.FlipHorizontally()); break;
            case PinCommand.FlipVertical: Apply(_state.FlipVertically()); break;
            case PinCommand.ToggleGrayscale: Apply(_state.ToggleGrayscale()); break;
            case PinCommand.ToggleInvert: Apply(_state.ToggleInvert()); break;
            case PinCommand.ZoomIn: Apply(_state.ZoomBy(ZoomStep)); break;
            case PinCommand.ZoomOut: Apply(_state.ZoomBy(1 / ZoomStep)); break;
            case PinCommand.OpacityUp: ForGroup(pin => pin.Apply(pin._state.WithOpacityStep(1))); break;
            case PinCommand.OpacityDown: ForGroup(pin => pin.Apply(pin._state.WithOpacityStep(-1))); break;
            case PinCommand.Copy: CopyImage(); break;
            case PinCommand.CopyAsFile: CopyAsFile(); break;
            case PinCommand.Save: SaveAs(); break;
            case PinCommand.QuickSave: QuickSave(); break;
            case PinCommand.Print: Print(); break;
            case PinCommand.OpenSettings: PinManager.RequestSettings(); break;
            case PinCommand.Move: MoveBy(ctx.Key, ctx.IsRepeat); break;
            case PinCommand.NudgeCursor: NudgeCursor(ctx.Key, ctx.IsRepeat); break;
            case PinCommand.OpenMenu: OpenMenu(); break;
            case PinCommand.PasteClipboard: PasteClipboard(); break;
            case PinCommand.CopyText: CopyText(); break;
            case PinCommand.SelectAll: PinManager.SelectAllVisible(); break;
            case PinCommand.ToggleLoupe: SetLoupe(!_loupeOn); break;
            case PinCommand.CopyLoupeColor: CopyLoupeColour(); break;
            case PinCommand.CycleLoupeFormat: CycleLoupeFormat(); break;
        }
    }

    /// <summary>Ctrl+V: show what the clipboard holds instead, keeping the turn, zoom and place
    /// (a text pin is drawn for this pin's monitor).</summary>
    private void PasteClipboard()
    {
        if (ClipboardPinSource.FromClipboard(_dpi) is { } content) ReplaceContent(content);
        else Warn(Loc.T("paste.nothing"));
    }

    /// <summary>Ctrl+Shift+C: the text a text or colour pin was made from, as plain text.</summary>
    private void CopyText()
    {
        if (_content.SourceText is { } text) ClipboardService.CopyText(text);
    }

    /// <summary>An arrow: move the pin, and every pin picked with it, a pixel, faster while
    /// the key is held. A drag under way owns the position (DragMove let no key through either).</summary>
    private void MoveBy(Key key, bool isRepeat)
    {
        if (_move != null || _resize != null) return;
        int step = CursorNudge.Step(NextNudgeRepeat(isRepeat));
        var (dx, dy) = CursorNudge.Direction(key);
        ForGroup(pin => pin.MoveWindowBy(dx * step, dy * step));
        UpdateLoupe();   // another pixel is under the cursor now
    }

    /// <summary>W/A/S/D: move the cursor a pixel, faster while the key is held, to pick a
    /// colour off the pin exactly. A drag follows the cursor, so with the left button held
    /// they move the pin a pixel at a time; SetCursorPos posts a mouse move too, but
    /// following at once keeps up with a held key.</summary>
    private void NudgeCursor(Key key, bool isRepeat)
    {
        int step = CursorNudge.Step(NextNudgeRepeat(isRepeat));
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        var (dx, dy) = CursorNudge.Direction(key);
        var (x, y) = CursorNudge.Clamp(pt.X + dx * step, pt.Y + dy * step, VirtualScreen.Current);
        if (x == pt.X && y == pt.Y) return;
        NativeMethods.SetCursorPos(x, y);
        FollowCursor();
    }

    private int NextNudgeRepeat(bool isRepeat) =>
        _nudgeRepeat = isRepeat ? Math.Min(_nudgeRepeat + 1, MaxNudgeRepeat) : 0;

    /// <summary>What the key types on the current keyboard layout with no modifier held, or
    /// null: none, or a dead key waiting for its letter.</summary>
    private static string? LayoutText(Key key)
    {
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return null;
        uint mapped = NativeMethods.MapVirtualKey((uint)vk, NativeMethods.MAPVK_VK_TO_CHAR);
        if (mapped == 0 || (mapped & DeadKeyFlag) != 0) return null;
        return ((char)(mapped & 0xFFFF)).ToString();
    }

    private static bool IsAltKey(Key key) => key is Key.LeftAlt or Key.RightAlt;
}
