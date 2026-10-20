using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>The capture overlay's two phases: framing a region, then annotating it.</summary>
public enum OverlayPhase { Selecting, Editing }

/// <summary>
/// What a key press in the capture overlay does. This is the whole capture key table;
/// <see cref="OverlayKeyRouter"/> maps the rows wired so far, and a command no row yields
/// yet does nothing when executed.
/// </summary>
public enum OverlayCommand
{
    /// <summary>No binding: the key is left unhandled.</summary>
    None,
    /// <summary>Deliberately left to the focused control (the text box, the font list).</summary>
    PassThrough,

    // ---- Esc chain / text box
    CommitText,          // Esc or Enter while typing ends the text edit
    CancelFramedDrag,    // Esc during a magnifier framed-source drag
    Deselect,            // Esc with an annotation selected
    ConfirmClose,        // Esc while editing: leave, asking first when that setting is on
    Close,               // Esc while selecting: leave at once

    // ---- Loupe (pixel magnifier)
    CopyLoupeColor,      // C
    CycleLoupeFormat,    // Shift tap

    // ---- Selecting phase
    ToggleDetectMode,    // Tab: UI element / window detection
    CycleScreenArea,     // Ctrl+A: active screen, then all screens
    NudgeCursor,         // W/A/S/D, and the arrows before a region is chosen
    AcceptDetected,      // Space: take the detected area
    HoldToMove,          // Space held mid-drag: move the area instead of resizing it
    ToggleGuideLines,    // Alt tap

    // ---- Region (editing phase)
    MoveRegion,          // arrows
    GrowRegion,          // Ctrl+arrows
    ShrinkRegion,        // Shift+arrows

    // ---- Outputs
    Copy,                // the Copy gesture, or Ctrl+Insert
    Save,
    QuickSave,
    Pin,                 // Ctrl+T
    Print,               // Ctrl+P
    CopyAsFile,          // Ctrl+Alt+C
    CopySize,            // V
    RunEnterAction,      // Enter outside a text box
    ToggleToolbar,       // Space

    // ---- Annotations
    Undo,
    Redo,
    ClearAll,            // Ctrl+Shift+Z
    DeleteSelected,      // Delete
    MoveAnnotation,      // arrows with an annotation selected
    SelectToolByLetter,  // T / B / M / E
    SelectToolByNumber,  // Ctrl+1..9, Ctrl+0
    NextTool,            // Ctrl+Tab
    PreviousTool,        // Ctrl+Shift+Tab
    ToggleToolVariant,   // Tab: rectangle/ellipse, line/arrow
    PenSizeDown,         // 1, [
    PenSizeUp,           // 2, ]
    PickColor,           // Alt+C
    NumberDown,          // - (number tool)
    NumberUp,            // + (number tool)
    CopyAnnotation,      // Ctrl+Shift+C
    PasteAnnotation,     // Ctrl+Shift+V
    FinishLineStrip,     // Esc or Enter while clicking out a line strip

    // ---- Snip context
    ToggleCursor,        // ` or !
    PreviousRegion,      // R
    NextRegion,          // Shift+R
    HistoryPrevious,     // ,
    HistoryNext,         // .
    RefreshScreenshot,   // F5
}

/// <summary>Everything about the overlay's state that decides what a key does.</summary>
public readonly record struct OverlayKeyContext
{
    public OverlayPhase Phase { get; init; }
    /// <summary>The effective key (<see cref="KeyInputNormalizer"/>).</summary>
    public Key Key { get; init; }
    public ModifierKeys Modifiers { get; init; }
    /// <summary>A keyboard auto-repeat of a held key.</summary>
    public bool IsRepeat { get; init; }
    /// <summary>WPF delivered the key as ImeProcessed: an IME composition owns it.</summary>
    public bool ImeProcessed { get; init; }
    /// <summary>A text annotation is being typed.</summary>
    public bool TextBoxActive { get; init; }
    /// <summary>The key already went through the text box, which left it unhandled.</summary>
    public bool TextBoxDeclined { get; init; }
    /// <summary>The toolbar's font list is open and navigating its items.</summary>
    public bool DropDownOpen { get; init; }
    public bool LoupeVisible { get; init; }
    /// <summary>An annotation is selected.</summary>
    public bool HasSelection { get; init; }
    /// <summary>A magnifier's framed source is being moved or resized.</summary>
    public bool FramedSourceDragging { get; init; }
    /// <summary>The left button is held: framing the area, resizing or moving it, drawing.</summary>
    public bool Dragging { get; init; }
    /// <summary>Selecting: hovering has a window or UI element highlighted.</summary>
    public bool HasDetectedArea { get; init; }
    /// <summary>Editing: the active annotation tool (None: moving the region).</summary>
    public ToolKind Tool { get; init; }
    /// <summary>Editing: the line tool is clicking out a strip.</summary>
    public bool LineStripActive { get; init; }
}

/// <summary>The user-configurable gestures (settings.json); null means unbound. Read with
/// <see cref="KeyChord.ParseGesture"/>, so a bare letter or digit stays unbound.</summary>
public sealed record OverlayKeyBindings(
    KeyChord? Undo, KeyChord? Redo, KeyChord? Copy, KeyChord? Save, KeyChord? QuickSave,
    KeyChord? Pin, KeyChord? Print, KeyChord? ClearAll)
{
    public static OverlayKeyBindings FromSettings(AppSettings s) => new(
        KeyChord.ParseGesture(s.UndoHotkey),
        KeyChord.ParseGesture(s.RedoHotkey),
        KeyChord.ParseGesture(s.CopyHotkey),
        KeyChord.ParseGesture(s.SaveHotkey),
        KeyChord.ParseGesture(s.QuickSaveHotkey),
        KeyChord.ParseGesture(s.PinHotkey),
        KeyChord.ParseGesture(s.PrintHotkey),
        KeyChord.ParseGesture(s.ClearAllHotkey));
}

/// <summary>
/// Maps a key press in the capture overlay to an <see cref="OverlayCommand"/>. Pure, so the
/// precedence rules are unit tested; the window builds the context from its state and runs
/// the result. Snipaste's fixed in-app keys are rows here; the configurable gestures come
/// from <see cref="OverlayKeyBindings"/>.
/// </summary>
public sealed class OverlayKeyRouter
{
    // static readonly, not const: coverlet cannot resolve WindowsBase enums in constants
    // and would skip instrumenting the whole app.
    private static readonly ModifierKeys ChordModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;
    private static readonly ModifierKeys CopyAsFileModifiers = ModifierKeys.Control | ModifierKeys.Alt;
    private static readonly ModifierKeys CtrlShiftModifiers = ModifierKeys.Control | ModifierKeys.Shift;

    private readonly OverlayKeyBindings _bindings;

    public OverlayKeyRouter(OverlayKeyBindings bindings) => _bindings = bindings;

    /// <summary>
    /// The fixed rows' chords that a configurable gesture or a global hotkey could take
    /// over: those with a modifier, and F5 (a global hotkey may be a bare function key). The
    /// settings window warns about a gesture on one of them; a test keeps the list true to
    /// the table.
    /// </summary>
    public static IReadOnlyList<KeyChord> FixedChords { get; } = BuildFixedChords();

    private static List<KeyChord> BuildFixedChords()
    {
        var chords = new List<KeyChord>
        {
            new(Key.A, ModifierKeys.Control),             // the screen area
            new(Key.Insert, ModifierKeys.Control),        // copy
            new(Key.C, CopyAsFileModifiers),              // copy as a file
            new(Key.C, ModifierKeys.Alt),                 // pick the colour
            new(Key.Tab, ModifierKeys.Control),           // next tool
            new(Key.Tab, CtrlShiftModifiers),             // previous tool
            new(Key.C, CtrlShiftModifiers),               // copy the annotation
            new(Key.V, CtrlShiftModifiers),               // paste it
            new(Key.R, ModifierKeys.Shift),               // the next newer area
            new(Key.F5, ModifierKeys.None),               // a fresh screenshot
        };
        for (var key = Key.D0; key <= Key.D9; key++) chords.Add(new(key, ModifierKeys.Control));   // tools by number
        foreach (var arrow in new[] { Key.Left, Key.Up, Key.Right, Key.Down })
        {
            chords.Add(new(arrow, ModifierKeys.Control));   // grow the region
            chords.Add(new(arrow, ModifierKeys.Shift));     // shrink it
        }
        return chords;
    }

    public OverlayCommand Route(OverlayKeyContext ctx)
    {
        if (ctx.DropDownOpen) return OverlayCommand.PassThrough;
        if (ctx.TextBoxActive) return RouteWhileTyping(ctx);
        if (ctx.Key == Key.Escape) return RouteEscape(ctx);

        if (ctx.LoupeVisible && ctx.Key == Key.C && ctx.Modifiers == ModifierKeys.None)
            return OverlayCommand.CopyLoupeColor;

        if (ctx.Phase != OverlayPhase.Editing) return RouteSelecting(ctx);

        // Enter ends a line strip (as Esc does) before anything else it could mean.
        if (ctx.LineStripActive && ctx.Key == Key.Enter && ctx.Modifiers == ModifierKeys.None)
            return OverlayCommand.FinishLineStrip;
        if (ctx.Key == Key.Delete && ctx.HasSelection) return OverlayCommand.DeleteSelected;
        var gesture = RouteGesture(ctx);
        return gesture != OverlayCommand.None ? gesture : RouteEditing(ctx);
    }

    /// <summary>A modifier pressed and released on its own (<see cref="ModifierTapTracker"/>).</summary>
    public OverlayCommand RouteTap(ModifierKeys tapped, OverlayKeyContext ctx)
    {
        if (ctx.DropDownOpen || ctx.TextBoxActive) return OverlayCommand.None;
        if (tapped == ModifierKeys.Shift && ctx.LoupeVisible) return OverlayCommand.CycleLoupeFormat;
        if (tapped == ModifierKeys.Alt && ctx.Phase == OverlayPhase.Selecting) return OverlayCommand.ToggleGuideLines;
        return OverlayCommand.None;
    }

    /// <summary>
    /// A typed character (TextInput), for the keys whose place depends on the keyboard layout,
    /// whatever modifiers typed them (Shift, or AltGr on many European layouts): in either
    /// phase ` and ! show or hide the captured pointer and , and . replay the snip history;
    /// while editing [ and ] step the pen size, and + and - change the number marker's next
    /// number. Only characters no key-down claimed arrive here.
    /// </summary>
    public OverlayCommand RouteText(string text, OverlayKeyContext ctx)
    {
        if (ctx.DropDownOpen || ctx.TextBoxActive) return OverlayCommand.PassThrough;
        if (ctx.Dragging) return OverlayCommand.None;
        var snip = SnipText(text);
        if (snip != OverlayCommand.None) return snip;
        if (ctx.Phase != OverlayPhase.Editing) return OverlayCommand.None;
        if (ctx.Tool == ToolKind.Number)
        {
            int step = ToolShortcuts.NumberStepDirection(text);
            if (step != 0) return step > 0 ? OverlayCommand.NumberUp : OverlayCommand.NumberDown;
        }
        return PenSize(ToolShortcuts.PenSizeDirection(text));
    }

    /// <summary>
    /// Framing the area. Space takes the detected area, or mid-drag holds the area to move
    /// it; Tab, Ctrl+A and the snip context's keys wait until the button is up; W/A/S/D and
    /// the arrows nudge the cursor, which a drag follows.
    /// </summary>
    private static OverlayCommand RouteSelecting(OverlayKeyContext ctx)
    {
        if (ctx.Key == Key.Space && (ctx.Modifiers & ChordModifiers) == 0)
        {
            if (ctx.Dragging) return OverlayCommand.HoldToMove;
            if (ctx.HasDetectedArea && ctx.Modifiers == ModifierKeys.None) return OverlayCommand.AcceptDetected;
            return OverlayCommand.None;
        }
        if ((CursorNudge.IsWasd(ctx.Key) || CursorNudge.IsArrow(ctx.Key)) && IsNudge(ctx))
            return OverlayCommand.NudgeCursor;
        if (ctx.Dragging) return OverlayCommand.None;

        if (ctx.Key == Key.Tab && ctx.Modifiers == ModifierKeys.None) return OverlayCommand.ToggleDetectMode;
        if (IsSelectAll(ctx)) return OverlayCommand.CycleScreenArea;
        return RouteSnipKey(ctx);
    }

    /// <summary>Snipaste's fixed editing keys, tried after the configurable gestures so a
    /// gesture the user bound to the same chord wins. Only W/A/S/D and Space work while the
    /// left button is held.</summary>
    private static OverlayCommand RouteEditing(OverlayKeyContext ctx)
    {
        if (ctx.Key == Key.Space) return RouteEditingSpace(ctx);
        if (IsSelectAll(ctx) && !ctx.Dragging) return OverlayCommand.CycleScreenArea;
        if (CursorNudge.IsWasd(ctx.Key) && IsNudge(ctx)) return OverlayCommand.NudgeCursor;
        if (ctx.Dragging) return OverlayCommand.None;
        if (CursorNudge.IsArrow(ctx.Key)) return RouteRegionArrow(ctx);
        var tool = RouteTool(ctx);
        if (tool != OverlayCommand.None) return tool;
        var output = RouteOutput(ctx);
        return output != OverlayCommand.None ? output : RouteSnipKey(ctx);
    }

    /// <summary>The snip context's keys, in either phase once the button is up: R steps back
    /// through the areas of earlier snips, Shift+R the other way, and F5 (held: again and
    /// again) captures the screen afresh.</summary>
    private static OverlayCommand RouteSnipKey(OverlayKeyContext ctx)
    {
        if (ctx.Key == Key.R)
        {
            if (ctx.Modifiers == ModifierKeys.None) return OverlayCommand.PreviousRegion;
            return ctx.Modifiers == ModifierKeys.Shift ? OverlayCommand.NextRegion : OverlayCommand.None;
        }
        if (ctx.Key == Key.F5 && ctx.Modifiers == ModifierKeys.None) return OverlayCommand.RefreshScreenshot;
        return OverlayCommand.None;
    }

    /// <summary>` or ! toggles the pointer (two spellings, as ` is a dead key on many layouts);
    /// , and . step back and forth through the snip history.</summary>
    private static OverlayCommand SnipText(string text) => text switch
    {
        "`" or "!" => OverlayCommand.ToggleCursor,
        "," => OverlayCommand.HistoryPrevious,
        "." => OverlayCommand.HistoryNext,
        _ => OverlayCommand.None,
    };

    /// <summary>
    /// The annotation tool keys (<see cref="ToolShortcuts"/>): T/B/M/E pick a tool, Ctrl+digit
    /// picks one by Snipaste's meaning, Ctrl+Tab and Ctrl+Shift+Tab cycle, Tab flips the
    /// current tool's variant (otherwise it still moves the focus), 1/2 step the width, Alt+C
    /// picks the colour under the cursor, and Ctrl+Shift+C / Ctrl+Shift+V copy the selected
    /// annotation and paste a copy of it.
    /// </summary>
    private static OverlayCommand RouteTool(OverlayKeyContext ctx)
    {
        if (ctx.Modifiers == ModifierKeys.None)
        {
            if (ToolShortcuts.ToolForLetter(ctx.Key) != null) return OverlayCommand.SelectToolByLetter;
            if (ctx.Key == Key.Tab)
                return ToolShortcuts.HasVariant(ctx.Tool) ? OverlayCommand.ToggleToolVariant : OverlayCommand.None;
            return PenSize(ToolShortcuts.PenSizeDirection(ctx.Key));
        }
        if (ctx.Modifiers == ModifierKeys.Control)
        {
            if (ToolShortcuts.DigitOf(ctx.Key) != null) return OverlayCommand.SelectToolByNumber;
            if (ctx.Key == Key.Tab) return OverlayCommand.NextTool;
            return OverlayCommand.None;
        }
        if (ctx.Modifiers == CtrlShiftModifiers)
        {
            if (ctx.Key == Key.Tab) return OverlayCommand.PreviousTool;
            if (ctx.Key == Key.C) return ctx.HasSelection ? OverlayCommand.CopyAnnotation : OverlayCommand.None;
            if (ctx.Key == Key.V) return OverlayCommand.PasteAnnotation;
            return OverlayCommand.None;
        }
        if (ctx.Key == Key.C && ctx.Modifiers == ModifierKeys.Alt) return OverlayCommand.PickColor;
        return OverlayCommand.None;
    }

    private static OverlayCommand PenSize(int direction) => direction switch
    {
        < 0 => OverlayCommand.PenSizeDown,
        > 0 => OverlayCommand.PenSizeUp,
        _ => OverlayCommand.None,
    };

    /// <summary>Space shows or hides the toolbar; mid-drag it holds the area to move it.</summary>
    private static OverlayCommand RouteEditingSpace(OverlayKeyContext ctx)
    {
        if (ctx.Dragging)
            return (ctx.Modifiers & ChordModifiers) == 0 ? OverlayCommand.HoldToMove : OverlayCommand.None;
        return ctx.Modifiers == ModifierKeys.None ? OverlayCommand.ToggleToolbar : OverlayCommand.None;
    }

    /// <summary>The fixed output keys: Enter runs the configurable Enter action, V copies the
    /// area's size, Ctrl+Insert copies (an alias that does not follow the Copy gesture) and
    /// Ctrl+Alt+C copies the capture as a file.</summary>
    private static OverlayCommand RouteOutput(OverlayKeyContext ctx)
    {
        if (ctx.Modifiers == ModifierKeys.None)
        {
            if (ctx.Key == Key.Enter) return OverlayCommand.RunEnterAction;
            if (ctx.Key == Key.V) return OverlayCommand.CopySize;
            return OverlayCommand.None;
        }
        if (ctx.Key == Key.Insert && ctx.Modifiers == ModifierKeys.Control) return OverlayCommand.Copy;
        if (ctx.Key == Key.C && ctx.Modifiers == CopyAsFileModifiers) return OverlayCommand.CopyAsFile;
        return OverlayCommand.None;
    }

    /// <summary>The arrows adjust the chosen region a pixel at a time (RegionNudge): plain
    /// arrows move it, or the selected annotation when there is one; Ctrl grows and Shift
    /// shrinks the region on the arrow's side, whatever is selected or whichever tool is active.</summary>
    private static OverlayCommand RouteRegionArrow(OverlayKeyContext ctx)
    {
        if (ctx.Modifiers == ModifierKeys.None)
            return ctx.HasSelection ? OverlayCommand.MoveAnnotation : OverlayCommand.MoveRegion;
        if (ctx.Modifiers == ModifierKeys.Control) return OverlayCommand.GrowRegion;
        if (ctx.Modifiers == ModifierKeys.Shift) return OverlayCommand.ShrinkRegion;
        return OverlayCommand.None;
    }

    /// <summary>A bare nudge key, or Shift+key mid-drag so a Shift+drag square can be
    /// fine-tuned too.</summary>
    private static bool IsNudge(OverlayKeyContext ctx) =>
        ctx.Modifiers == ModifierKeys.None || (ctx.Dragging && ctx.Modifiers == ModifierKeys.Shift);

    private static bool IsSelectAll(OverlayKeyContext ctx) =>
        ctx.Key == Key.A && ctx.Modifiers == ModifierKeys.Control;

    /// <summary>
    /// The text box sees keys first: Esc and Enter end the edit, Shift+Enter is a new line,
    /// and everything else is typing. A gesture the box then leaves unhandled (Ctrl+S, or
    /// Ctrl+Z with nothing left to undo in the box) still runs, but only when it holds Ctrl,
    /// Alt or Win, so a bare-letter binding can never fire mid-word.
    /// </summary>
    private OverlayCommand RouteWhileTyping(OverlayKeyContext ctx)
    {
        if (ctx.ImeProcessed) return OverlayCommand.PassThrough;   // a CJK composition owns Enter/Esc

        if (!ctx.TextBoxDeclined)
        {
            if (ctx.Key == Key.Escape) return OverlayCommand.CommitText;
            if (ctx.Key == Key.Enter && !ctx.Modifiers.HasFlag(ModifierKeys.Shift)) return OverlayCommand.CommitText;
            return OverlayCommand.PassThrough;
        }

        if ((ctx.Modifiers & ChordModifiers) == 0) return OverlayCommand.PassThrough;
        var gesture = RouteGesture(ctx);
        return gesture == OverlayCommand.None ? OverlayCommand.PassThrough : gesture;
    }

    /// <summary>Snipaste-style: cancel a framed-source drag, then finish a line strip, then
    /// clear the selection, then leave the capture (confirming when editing), with no
    /// reselect step. Selecting leaves at once. The drag goes first because it owns the mouse
    /// and Deselect hides its handles, so it would otherwise keep resizing invisibly.</summary>
    private static OverlayCommand RouteEscape(OverlayKeyContext ctx)
    {
        if (ctx.Phase == OverlayPhase.Selecting) return OverlayCommand.Close;
        if (ctx.FramedSourceDragging) return OverlayCommand.CancelFramedDrag;
        if (ctx.LineStripActive) return OverlayCommand.FinishLineStrip;
        if (ctx.HasSelection) return OverlayCommand.Deselect;
        return OverlayCommand.ConfirmClose;
    }

    /// <summary>The configurable gestures. QuickSave is tried before Save so a binding that
    /// extends Save's (Ctrl+Shift+S over Ctrl+S) is never shadowed, and the destructive
    /// ClearAll comes after Undo/Redo, so it loses a tie and waits while a drag owns the
    /// annotations.</summary>
    private OverlayCommand RouteGesture(OverlayKeyContext ctx)
    {
        if (Is(_bindings.QuickSave, ctx)) return OverlayCommand.QuickSave;
        if (Is(_bindings.Undo, ctx)) return OverlayCommand.Undo;
        if (Is(_bindings.Redo, ctx)) return OverlayCommand.Redo;
        if (Is(_bindings.Copy, ctx)) return OverlayCommand.Copy;
        if (Is(_bindings.Save, ctx)) return OverlayCommand.Save;
        if (Is(_bindings.Pin, ctx)) return OverlayCommand.Pin;
        if (Is(_bindings.Print, ctx)) return OverlayCommand.Print;
        if (!ctx.Dragging && Is(_bindings.ClearAll, ctx)) return OverlayCommand.ClearAll;
        return OverlayCommand.None;
    }

    private static bool Is(KeyChord? chord, OverlayKeyContext ctx) =>
        chord is { } c && c.Matches(ctx.Key, ctx.Modifiers);
}
