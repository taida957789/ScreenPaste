using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>What a key press on a focused pin does.</summary>
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
    Copy,                    // the Copy gesture, or Ctrl+Insert
    CopyAsFile,              // Ctrl+Alt+C
    Save,                    // the Save gesture
    QuickSave,               // the Quick save gesture
    Print,                   // the Print gesture
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
    /// ("=" for the US +/= key), or null; + and - are matched by it.</summary>
    public string? Text { get; init; }
    /// <summary>A keyboard auto-repeat of a held key.</summary>
    public bool IsRepeat { get; init; }
    /// <summary>The pin's loupe is on and showing a pixel, so C copies its colour.</summary>
    public bool LoupeVisible { get; init; }
}

/// <summary>The configurable gestures a pin follows (settings.json); null means unbound.
/// Read with <see cref="KeyChord.ParseGesture"/>, so a bare letter stays unbound.</summary>
public sealed record PinKeyBindings(KeyChord? Copy, KeyChord? Save, KeyChord? QuickSave, KeyChord? Print)
{
    public static PinKeyBindings FromSettings(AppSettings s) => new(
        KeyChord.ParseGesture(s.CopyHotkey),
        KeyChord.ParseGesture(s.SaveHotkey),
        KeyChord.ParseGesture(s.QuickSaveHotkey),
        KeyChord.ParseGesture(s.PrintHotkey));
}

/// <summary>
/// Maps a key press on a focused pin to a <see cref="PinCommand"/> (Snipaste's pin keys).
/// Pure, so the table is unit tested; <see cref="PinWindow"/> runs the result. The
/// configurable gestures are tried first, so one bound to a fixed key's chord wins.
/// </summary>
public sealed class PinKeyRouter
{
    // static readonly, not const: coverlet cannot resolve WindowsBase enums in constants
    // and would skip instrumenting the whole app.
    private static readonly ModifierKeys CtrlAlt = ModifierKeys.Control | ModifierKeys.Alt;
    private static readonly ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;
    private static readonly KeyChord CopyAliasChord = new(Key.Insert, ModifierKeys.Control);
    private static readonly KeyChord CopyAsFileChord = new(Key.C, CtrlAlt);
    private static readonly KeyChord OpenSettingsChord = new(Key.P, CtrlShift);
    private static readonly KeyChord CloseChord = new(Key.W, ModifierKeys.Control);
    private static readonly KeyChord DestroyChord = new(Key.Escape, ModifierKeys.Shift);
    private static readonly KeyChord PasteChord = new(Key.V, ModifierKeys.Control);
    private static readonly KeyChord CopyTextChord = new(Key.C, CtrlShift);
    private static readonly KeyChord SelectAllChord = new(Key.A, ModifierKeys.Control);

    private readonly PinKeyBindings _bindings;

    public PinKeyRouter(PinKeyBindings bindings) => _bindings = bindings;

    /// <summary>The fixed chords a configurable gesture or a global hotkey could take over;
    /// the settings window warns about a gesture on one of them.</summary>
    public static IReadOnlyList<KeyChord> FixedChords { get; } = new[]
    {
        CopyAliasChord, CopyAsFileChord, OpenSettingsChord, CloseChord, DestroyChord, PasteChord, CopyTextChord,
        SelectAllChord,
    };

    public PinCommand Route(PinKeyContext ctx)
    {
        // Esc closes whatever else is held with it, as it always did; only Shift+Esc destroys.
        if (ctx.Key == Key.Escape)
            return DestroyChord.Matches(ctx.Key, ctx.Modifiers) ? PinCommand.Destroy : PinCommand.Close;

        var gesture = RouteGesture(ctx);
        if (gesture != PinCommand.None) return gesture;

        var chord = RouteChord(ctx);
        if (chord != PinCommand.None) return chord;

        if (ctx.Modifiers == ModifierKeys.None)
        {
            if (ctx.LoupeVisible && ctx.Key == Key.C) return PinCommand.CopyLoupeColor;
            var bare = RouteBareKey(ctx.Key);
            if (bare != PinCommand.None) return bare;
        }
        return RouteZoomKey(ctx);
    }

    /// <summary>A modifier pressed and released on its own (<see cref="ModifierTapTracker"/>):
    /// Alt turns the loupe on and off; Shift, while it shows, cycles its colour format.</summary>
    public static PinCommand RouteTap(ModifierKeys tapped, PinKeyContext ctx)
    {
        if (tapped == ModifierKeys.Alt) return PinCommand.ToggleLoupe;
        if (tapped == ModifierKeys.Shift && ctx.LoupeVisible) return PinCommand.CycleLoupeFormat;
        return PinCommand.None;
    }

    /// <summary>Whether a held key's auto-repeats run <paramref name="command"/> again: zoom,
    /// opacity and moves keep going; turns, flips, toggles and outputs happen once per press.</summary>
    public static bool RunsOnRepeat(PinCommand command) => command is
        PinCommand.ZoomIn or PinCommand.ZoomOut or PinCommand.OpacityUp or PinCommand.OpacityDown or
        PinCommand.Move or PinCommand.NudgeCursor;

    /// <summary>The fixed keys of <paramref name="command"/> as the pin's menu shows them, kept
    /// next to the table so the two cannot drift apart; null for a command that follows a
    /// gesture in the settings (the menu shows that one) or has no menu row.</summary>
    public static string? FixedKeys(PinCommand command) => command switch
    {
        PinCommand.Close => "Esc",
        PinCommand.Destroy => DestroyChord.ToString(),
        PinCommand.RotateClockwise => "1",
        PinCommand.RotateCounterClockwise => "2",
        PinCommand.FlipHorizontal => "3",
        PinCommand.FlipVertical => "4",
        PinCommand.ToggleGrayscale => "5",
        PinCommand.ToggleInvert => "6",
        PinCommand.OpacityUp => "Ctrl++",
        PinCommand.OpacityDown => "Ctrl+-",
        PinCommand.CopyAsFile => CopyAsFileChord.ToString(),
        PinCommand.OpenSettings => OpenSettingsChord.ToString(),
        PinCommand.PasteClipboard => PasteChord.ToString(),
        PinCommand.CopyText => CopyTextChord.ToString(),
        _ => null,
    };

    /// <summary>QuickSave before Save, so a binding that extends Save's is never shadowed.</summary>
    private PinCommand RouteGesture(PinKeyContext ctx)
    {
        if (Is(_bindings.QuickSave, ctx)) return PinCommand.QuickSave;
        if (Is(_bindings.Copy, ctx)) return PinCommand.Copy;
        if (Is(_bindings.Save, ctx)) return PinCommand.Save;
        if (Is(_bindings.Print, ctx)) return PinCommand.Print;
        return PinCommand.None;
    }

    /// <summary>The fixed chords: Ctrl+Insert copies (an alias that does not follow the Copy
    /// gesture), Ctrl+Alt+C copies as a file, Ctrl+Shift+P opens the settings, Ctrl+W closes,
    /// Ctrl+V shows the clipboard instead, Ctrl+Shift+C copies a text pin's text and Ctrl+A
    /// picks every shown pin.</summary>
    private static PinCommand RouteChord(PinKeyContext ctx)
    {
        if (CopyAliasChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.Copy;
        if (CopyAsFileChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.CopyAsFile;
        if (OpenSettingsChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.OpenSettings;
        if (CloseChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.Close;
        if (PasteChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.PasteClipboard;
        if (CopyTextChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.CopyText;
        if (SelectAllChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.SelectAll;
        return PinCommand.None;
    }

    /// <summary>1-6 by key (whatever they type on the layout), the arrows, W/A/S/D and the Menu key.</summary>
    private static PinCommand RouteBareKey(Key key)
    {
        switch (key)
        {
            case Key.D1 or Key.NumPad1: return PinCommand.RotateClockwise;
            case Key.D2 or Key.NumPad2: return PinCommand.RotateCounterClockwise;
            case Key.D3 or Key.NumPad3: return PinCommand.FlipHorizontal;
            case Key.D4 or Key.NumPad4: return PinCommand.FlipVertical;
            case Key.D5 or Key.NumPad5: return PinCommand.ToggleGrayscale;
            case Key.D6 or Key.NumPad6: return PinCommand.ToggleInvert;
            case Key.Apps: return PinCommand.OpenMenu;
        }
        if (CursorNudge.IsArrow(key)) return PinCommand.Move;
        if (CursorNudge.IsWasd(key)) return PinCommand.NudgeCursor;
        return PinCommand.None;
    }

    /// <summary>+ (or =, the same key unshifted on many layouts) and - zoom, Shift or not;
    /// with Ctrl they step the opacity instead.</summary>
    private static PinCommand RouteZoomKey(PinKeyContext ctx)
    {
        int direction = ZoomDirection(ctx);
        if (direction == 0) return PinCommand.None;

        var withoutShift = ctx.Modifiers & ~ModifierKeys.Shift;
        if (withoutShift == ModifierKeys.None) return direction > 0 ? PinCommand.ZoomIn : PinCommand.ZoomOut;
        if (withoutShift == ModifierKeys.Control) return direction > 0 ? PinCommand.OpacityUp : PinCommand.OpacityDown;
        return PinCommand.None;
    }

    /// <summary>+1 for + or =, -1 for -, else 0: by the typed character, or the numpad's own keys.</summary>
    private static int ZoomDirection(PinKeyContext ctx)
    {
        switch (ctx.Text)
        {
            case "+" or "=": return 1;
            case "-": return -1;
        }
        return ctx.Key switch
        {
            Key.Add => 1,
            Key.Subtract => -1,
            _ => 0,
        };
    }

    private static bool Is(KeyChord? chord, PinKeyContext ctx) =>
        chord is { } c && c.Matches(ctx.Key, ctx.Modifiers);
}
