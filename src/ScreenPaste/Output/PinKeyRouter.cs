using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>What a key press on a focused pin does. The keys are the user's
/// (<see cref="ShortcutMap"/>); the comments give Snipaste's defaults.</summary>
public enum PinCommand
{
    /// <summary>No binding: the key is left unhandled.</summary>
    None,
    Close,                   // Esc or Ctrl+W: recoverably, the paste hotkey brings it back
    Destroy,                 // Shift+Esc: for good
    RotateClockwise,         // 1
    RotateCounterClockwise,  // 2
    FlipHorizontal,          // 3
    FlipVertical,            // 4
    ToggleGrayscale,         // 5
    ToggleInvert,            // 6
    ZoomIn,                  // +
    ZoomOut,                 // -
    OpacityUp,               // Ctrl++
    OpacityDown,             // Ctrl+-
    Copy,                    // Ctrl+C, Ctrl+Insert
    CopyAsFile,              // Ctrl+Alt+C
    Save,                    // Ctrl+S
    QuickSave,               // Ctrl+Shift+S
    Print,                   // Ctrl+P
    OpenSettings,            // Ctrl+Shift+P
    Move,                    // arrows: the pin, a pixel at a time
    NudgeCursor,             // W/A/S/D: the cursor, a pixel at a time
    OpenMenu,                // the Menu (Apps) key
    PasteClipboard,          // Ctrl+V: show what the clipboard holds instead
    CopyText,                // Ctrl+Shift+C: the text a text pin was made from
    SelectAll,               // Ctrl+A: pick every shown pin, to move, fade or close them together
    ToggleLoupe,             // an Alt tap: the magnifier and colour picker over the pin
    CopyLoupeColor,          // C, the loupe showing: the colour under the cursor, as written
    CycleLoupeFormat,        // a Shift tap, the loupe showing: HEX, RGB, HSV
}

/// <summary>A key press on a pin, as <see cref="PinKeyRouter"/> sees it.</summary>
public readonly record struct PinKeyContext
{
    /// <summary>The effective key (<see cref="KeyInputNormalizer"/>).</summary>
    public Key Key { get; init; }
    public ModifierKeys Modifiers { get; init; }
    /// <summary>What the key types on the current keyboard layout with no modifier held
    /// ("=" for the US +/= key), or null; character keys (+, -) are matched by it.</summary>
    public string? Text { get; init; }
    /// <summary>A keyboard auto-repeat of a held key.</summary>
    public bool IsRepeat { get; init; }
    /// <summary>The pin's loupe is on and showing a pixel, so C copies its colour.</summary>
    public bool LoupeVisible { get; init; }
}

/// <summary>A routed pin key: the command, and the action whose key it was, which tells a
/// move or nudge its direction (<see cref="ShortcutAction.Canonical"/>).</summary>
public readonly record struct PinRoute(PinCommand Command, ShortcutAction? Action)
{
    public static readonly PinRoute Nothing = new(PinCommand.None, null);
}

/// <summary>
/// Maps a key press, a typed character or a modifier tap on a focused pin to a
/// <see cref="PinCommand"/>, by the keys the user gave each action (<see cref="ShortcutMap"/>,
/// Snipaste's by default). Pure, so the table is unit tested; <see cref="PinWindow"/> runs the
/// result. The outputs are tried before the other keys, so one the user bound to another
/// action's key wins, as a gesture always did.
/// </summary>
public sealed class PinKeyRouter
{
    private static readonly (string Id, PinCommand Command)[] Outputs =
    {
        (PinShortcuts.QuickSave, PinCommand.QuickSave),   // before Save, so a binding that extends Save's wins
        (PinShortcuts.Copy, PinCommand.Copy),
        (PinShortcuts.Save, PinCommand.Save),
        (PinShortcuts.Print, PinCommand.Print),
    };

    private static readonly (string Id, PinCommand Command)[] Keys =
    {
        (PinShortcuts.CopyAsFile, PinCommand.CopyAsFile),
        (PinShortcuts.Settings, PinCommand.OpenSettings),
        (PinShortcuts.Close, PinCommand.Close),
        (PinShortcuts.Paste, PinCommand.PasteClipboard),
        (PinShortcuts.CopyText, PinCommand.CopyText),
        (PinShortcuts.SelectAll, PinCommand.SelectAll),
        (PinShortcuts.RotateClockwise, PinCommand.RotateClockwise),
        (PinShortcuts.RotateCounterClockwise, PinCommand.RotateCounterClockwise),
        (PinShortcuts.FlipHorizontal, PinCommand.FlipHorizontal),
        (PinShortcuts.FlipVertical, PinCommand.FlipVertical),
        (PinShortcuts.Grayscale, PinCommand.ToggleGrayscale),
        (PinShortcuts.Invert, PinCommand.ToggleInvert),
        (PinShortcuts.Menu, PinCommand.OpenMenu),
        (PinShortcuts.ZoomIn, PinCommand.ZoomIn),
        (PinShortcuts.ZoomOut, PinCommand.ZoomOut),
        (PinShortcuts.OpacityUp, PinCommand.OpacityUp),
        (PinShortcuts.OpacityDown, PinCommand.OpacityDown),
        (PinShortcuts.Loupe, PinCommand.ToggleLoupe),
    };

    private static readonly ShortcutKey PlainEsc = ShortcutKey.Chord(Key.Escape, ModifierKeys.None);

    private readonly ShortcutMap _keys;

    public PinKeyRouter(ShortcutMap keys) => _keys = keys;

    public PinCommand Route(PinKeyContext ctx) => Resolve(ctx).Command;

    /// <summary>A modifier pressed and released on its own (<see cref="ModifierTapTracker"/>):
    /// Alt turns the loupe on and off; Shift, while it shows, cycles its colour format.</summary>
    public PinCommand RouteTap(ModifierKeys tapped, PinKeyContext ctx) => ResolveTap(tapped, ctx).Command;

    public PinRoute ResolveTap(ModifierKeys tapped, PinKeyContext ctx) => Decide(KeyInput.Tapped(tapped), ctx);

    /// <summary>A typed character no key-down claimed (Shift+= typing +, an AltGr symbol).</summary>
    public PinCommand RouteText(string text, PinKeyContext ctx) => ResolveText(text, ctx).Command;

    public PinRoute Resolve(PinKeyContext ctx)
    {
        var input = KeyInput.Press(ctx.Key, ctx.Modifiers, ctx.Text);
        // Destroying goes first, then closing by Esc: whatever else is held with Esc (Ctrl,
        // Alt), it closes, as it always did, as long as Esc is a close key.
        if (Has(PinShortcuts.Destroy, input)) return Route(PinCommand.Destroy, PinShortcuts.Destroy);
        if (ctx.Key == Key.Escape && _keys.For(PinShortcuts.Close).Contains(PlainEsc)) return Route(PinCommand.Close, PinShortcuts.Close);
        return Decide(input, ctx);
    }

    public PinRoute ResolveText(string text, PinKeyContext ctx) => Decide(KeyInput.Typed(text), ctx);

    /// <summary>Whether a held key's auto-repeats run <paramref name="command"/> again: zoom,
    /// opacity and moves keep going; turns, flips, toggles and outputs happen once per press.</summary>
    public static bool RunsOnRepeat(PinCommand command) => command is
        PinCommand.ZoomIn or PinCommand.ZoomOut or PinCommand.OpacityUp or PinCommand.OpacityDown or
        PinCommand.Move or PinCommand.NudgeCursor;

    private PinRoute Decide(KeyInput input, PinKeyContext ctx)
    {
        if (Has(PinShortcuts.Destroy, input)) return Route(PinCommand.Destroy, PinShortcuts.Destroy);
        foreach (var (id, command) in Outputs)
            if (Has(id, input)) return Route(command, id);
        if (ctx.LoupeVisible && Has(PinShortcuts.CopyColor, input)) return Route(PinCommand.CopyLoupeColor, PinShortcuts.CopyColor);
        if (ctx.LoupeVisible && Has(PinShortcuts.ColorFormat, input)) return Route(PinCommand.CycleLoupeFormat, PinShortcuts.ColorFormat);
        foreach (var (id, command) in Keys)
            if (Has(id, input)) return Route(command, id);
        foreach (var id in PinShortcuts.Moves)
            if (Has(id, input)) return Route(PinCommand.Move, id);
        foreach (var id in PinShortcuts.Nudges)
            if (Has(id, input)) return Route(PinCommand.NudgeCursor, id);
        return PinRoute.Nothing;
    }

    private bool Has(string id, KeyInput input) => _keys.Matches(id, input);

    private static PinRoute Route(PinCommand command, string id) => new(command, ShortcutActions.ById(id));
}
