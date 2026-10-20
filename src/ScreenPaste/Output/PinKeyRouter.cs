using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>What a key press on a focused pin does.</summary>
public enum PinCommand
{
    /// <summary>No binding: the key is left unhandled.</summary>
    None,
    Close,                   // Esc
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

    private readonly PinKeyBindings _bindings;

    public PinKeyRouter(PinKeyBindings bindings) => _bindings = bindings;

    public PinCommand Route(PinKeyContext ctx)
    {
        // Today's behaviour, whatever is held with it.
        if (ctx.Key == Key.Escape) return PinCommand.Close;

        var gesture = RouteGesture(ctx);
        if (gesture != PinCommand.None) return gesture;

        var chord = RouteChord(ctx);
        if (chord != PinCommand.None) return chord;

        if (ctx.Modifiers == ModifierKeys.None)
        {
            var bare = RouteBareKey(ctx.Key);
            if (bare != PinCommand.None) return bare;
        }
        return RouteZoomKey(ctx);
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
    /// gesture), Ctrl+Alt+C copies as a file, Ctrl+Shift+P opens the settings.</summary>
    private static PinCommand RouteChord(PinKeyContext ctx)
    {
        if (CopyAliasChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.Copy;
        if (CopyAsFileChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.CopyAsFile;
        if (OpenSettingsChord.Matches(ctx.Key, ctx.Modifiers)) return PinCommand.OpenSettings;
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
