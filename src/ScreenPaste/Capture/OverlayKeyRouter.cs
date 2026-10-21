using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>The capture overlay's two phases: framing a region, then annotating it.</summary>
public enum OverlayPhase { Selecting, Editing }

/// <summary>
/// What a key press in the capture overlay does. The keys are the user's
/// (<see cref="ShortcutMap"/>); the comments give Snipaste's defaults.
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
    SelectTool,          // T / B / M / E, Ctrl+1..9, Ctrl+0: the tool of the action
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
    /// <summary>What the key types on the current layout with no modifier held, or null. Only
    /// a character bound with Ctrl, Alt or Win is matched by it; a plain one waits for the
    /// character typed (<see cref="OverlayKeyRouter.RouteText"/>), so Shift+, is not ",".</summary>
    public string? Text { get; init; }
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

/// <summary>A routed key: the command, and the action whose key it was (null for the fixed
/// text-box keys). The action tells a directional command its direction
/// (<see cref="ShortcutAction.Canonical"/>) and <see cref="OverlayCommand.SelectTool"/> its tool.</summary>
public readonly record struct OverlayRoute(OverlayCommand Command, ShortcutAction? Action)
{
    public static readonly OverlayRoute Nothing = new(OverlayCommand.None, null);
    public static readonly OverlayRoute Pass = new(OverlayCommand.PassThrough, null);
}

/// <summary>
/// Maps a key press, a typed character or a modifier tap in the capture overlay to an
/// <see cref="OverlayCommand"/>, by the keys the user gave each action (<see cref="ShortcutMap"/>,
/// Snipaste's by default). Pure, so the precedence rules are unit tested; the window builds the
/// context from its state and runs the result. Only the text box's own keys are fixed: Esc and
/// Enter end the edit, Shift+Enter is a new line.
/// </summary>
public sealed class OverlayKeyRouter
{
    // static readonly, not const: coverlet cannot resolve WindowsBase enums in constants
    // and would skip instrumenting the whole app.
    private static readonly ModifierKeys ChordModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;

    /// <summary>The gestures that act on the result, tried first while annotating and the
    /// only keys that still run while a text box is typed in (when they hold Ctrl, Alt or Win).
    /// QuickSave before Save, so a binding that extends Save's is never shadowed; the
    /// destructive ClearAll after Undo/Redo, so it loses a tie.</summary>
    private static readonly (string Id, OverlayCommand Command)[] Gestures =
    {
        (CaptureShortcuts.QuickSave, OverlayCommand.QuickSave),
        (CaptureShortcuts.Undo, OverlayCommand.Undo),
        (CaptureShortcuts.Redo, OverlayCommand.Redo),
        (CaptureShortcuts.Copy, OverlayCommand.Copy),
        (CaptureShortcuts.Save, OverlayCommand.Save),
        (CaptureShortcuts.Pin, OverlayCommand.Pin),
        (CaptureShortcuts.Print, OverlayCommand.Print),
        (CaptureShortcuts.ClearAll, OverlayCommand.ClearAll),
    };

    private readonly ShortcutMap _keys;

    public OverlayKeyRouter(ShortcutMap keys) => _keys = keys;

    public OverlayCommand Route(OverlayKeyContext ctx) => ResolveKey(ctx).Command;

    /// <summary>A modifier pressed and released on its own (<see cref="ModifierTapTracker"/>).</summary>
    public OverlayCommand RouteTap(ModifierKeys tapped, OverlayKeyContext ctx) => ResolveTap(tapped, ctx).Command;

    /// <summary>A typed character (TextInput), for the keys whose place depends on the keyboard
    /// layout, whatever modifiers typed them (Shift, or AltGr on many European layouts). Only
    /// characters no key-down claimed arrive here.</summary>
    public OverlayCommand RouteText(string text, OverlayKeyContext ctx) => ResolveText(text, ctx).Command;

    public OverlayRoute ResolveKey(OverlayKeyContext ctx)
    {
        if (ctx.DropDownOpen) return OverlayRoute.Pass;
        var input = KeyInput.Press(ctx.Key, ctx.Modifiers, (ctx.Modifiers & ChordModifiers) != 0 ? ctx.Text : null);
        return ctx.TextBoxActive ? RouteWhileTyping(ctx, input) : Decide(ctx, input);
    }

    public OverlayRoute ResolveTap(ModifierKeys tapped, OverlayKeyContext ctx) =>
        ctx.DropDownOpen || ctx.TextBoxActive ? OverlayRoute.Nothing : Decide(ctx, KeyInput.Tapped(tapped));

    public OverlayRoute ResolveText(string text, OverlayKeyContext ctx) =>
        ctx.DropDownOpen || ctx.TextBoxActive ? OverlayRoute.Pass : Decide(ctx, KeyInput.Typed(text));

    /// <summary>Whether releasing <paramref name="key"/> (which types <paramref name="layoutText"/>
    /// unshifted) lets go of one of <paramref name="ids"/>' keys: the hold-to-move key, or a move
    /// key whose held moves become one undo step.</summary>
    public bool Releases(IEnumerable<string> ids, Key key, string? layoutText = null) =>
        ids.Any(id => _keys.For(id).Any(k =>
            (k.Kind == ShortcutKeyKind.Chord && k.Key == key) ||
            (k.Kind == ShortcutKeyKind.Character && layoutText is { Length: 1 } && layoutText[0] == k.Character)));

    private OverlayRoute Decide(OverlayKeyContext ctx, KeyInput input)
    {
        if (HasWithAnyModifiers(CaptureShortcuts.Escape, Key.Escape, input)) return Route(Escape(ctx), CaptureShortcuts.Escape);
        if (ctx.LoupeVisible && Has(CaptureShortcuts.CopyColor, input)) return Route(OverlayCommand.CopyLoupeColor, CaptureShortcuts.CopyColor);
        if (ctx.LoupeVisible && Has(CaptureShortcuts.ColorFormat, input)) return Route(OverlayCommand.CycleLoupeFormat, CaptureShortcuts.ColorFormat);
        return ctx.Phase == OverlayPhase.Editing ? DecideEditing(ctx, input) : DecideSelecting(ctx, input);
    }

    /// <summary>
    /// Framing the area. Space takes the detected area, or mid-drag holds the area to move
    /// it; Tab, Ctrl+A, the guide lines and the snip context's keys wait until the button is
    /// up; W/A/S/D and the arrows nudge the cursor, which a drag follows.
    /// </summary>
    private OverlayRoute DecideSelecting(OverlayKeyContext ctx, KeyInput input)
    {
        if (ctx.Dragging && HasWhileDragging(CaptureShortcuts.HoldToMove, ctx, input))
            return Route(OverlayCommand.HoldToMove, CaptureShortcuts.HoldToMove);
        if (!ctx.Dragging && ctx.HasDetectedArea && Has(CaptureShortcuts.AcceptDetected, input))
            return Route(OverlayCommand.AcceptDetected, CaptureShortcuts.AcceptDetected);
        if (FirstWhileDragging(CaptureShortcuts.Nudges.Concat(CaptureShortcuts.Moves), ctx, input) is { } nudge)
            return new(OverlayCommand.NudgeCursor, nudge);
        if (ctx.Dragging) return OverlayRoute.Nothing;

        if (Has(CaptureShortcuts.DetectMode, input)) return Route(OverlayCommand.ToggleDetectMode, CaptureShortcuts.DetectMode);
        if (Has(CaptureShortcuts.ScreenArea, input)) return Route(OverlayCommand.CycleScreenArea, CaptureShortcuts.ScreenArea);
        if (Has(CaptureShortcuts.GuideLines, input)) return Route(OverlayCommand.ToggleGuideLines, CaptureShortcuts.GuideLines);
        return DecideSnip(input);
    }

    /// <summary>Annotating: the gestures first, so one the user bound to another action's key
    /// wins; only W/A/S/D, Space and the gestures work while the left button is held.</summary>
    private OverlayRoute DecideEditing(OverlayKeyContext ctx, KeyInput input)
    {
        // Enter ends a line strip (as Esc does) before anything else it could mean.
        if (ctx.LineStripActive && Has(CaptureShortcuts.EnterAction, input))
            return Route(OverlayCommand.FinishLineStrip, CaptureShortcuts.EnterAction);
        if (ctx.HasSelection && HasWithAnyModifiers(CaptureShortcuts.Delete, Key.Delete, input))
            return Route(OverlayCommand.DeleteSelected, CaptureShortcuts.Delete);
        var gesture = DecideGesture(ctx, input, typing: false);
        if (gesture.Command != OverlayCommand.None) return gesture;

        if (ctx.Dragging && HasWhileDragging(CaptureShortcuts.HoldToMove, ctx, input))
            return Route(OverlayCommand.HoldToMove, CaptureShortcuts.HoldToMove);
        if (!ctx.Dragging && Has(CaptureShortcuts.ToggleToolbar, input)) return Route(OverlayCommand.ToggleToolbar, CaptureShortcuts.ToggleToolbar);
        if (!ctx.Dragging && Has(CaptureShortcuts.ScreenArea, input)) return Route(OverlayCommand.CycleScreenArea, CaptureShortcuts.ScreenArea);
        if (FirstWhileDragging(CaptureShortcuts.Nudges, ctx, input) is { } nudge) return new(OverlayCommand.NudgeCursor, nudge);
        if (ctx.Dragging) return OverlayRoute.Nothing;

        var region = DecideRegion(ctx, input);
        if (region.Command != OverlayCommand.None) return region;
        var tool = DecideTool(ctx, input);
        if (tool.Command != OverlayCommand.None) return tool;
        var output = DecideOutput(input);
        return output.Command != OverlayCommand.None ? output : DecideSnip(input);
    }

    /// <summary>The arrows adjust the chosen region a pixel at a time (RegionNudge): the move
    /// keys move it, or the selected annotation when there is one; the grow and shrink keys
    /// change the region on their side, whatever is selected or whichever tool is active.</summary>
    private OverlayRoute DecideRegion(OverlayKeyContext ctx, KeyInput input)
    {
        if (First(CaptureShortcuts.Moves, input) is { } move)
            return new(ctx.HasSelection ? OverlayCommand.MoveAnnotation : OverlayCommand.MoveRegion, move);
        if (First(CaptureShortcuts.Grows, input) is { } grow) return new(OverlayCommand.GrowRegion, grow);
        if (First(CaptureShortcuts.Shrinks, input) is { } shrink) return new(OverlayCommand.ShrinkRegion, shrink);
        return OverlayRoute.Nothing;
    }

    /// <summary>
    /// The annotation tool keys (<see cref="ToolShortcuts"/>): a tool's keys pick it, Ctrl+Tab
    /// and Ctrl+Shift+Tab cycle, Tab flips the current tool's variant (otherwise it still moves
    /// the focus), 1/2 and [/] step the width, + and - the number marker's next number (with
    /// that tool), Alt+C picks the colour under the cursor, and Ctrl+Shift+C / Ctrl+Shift+V copy
    /// the selected annotation and paste a copy of it.
    /// </summary>
    private OverlayRoute DecideTool(OverlayKeyContext ctx, KeyInput input)
    {
        if (First(CaptureShortcuts.Tools, input) is { } tool) return new(OverlayCommand.SelectTool, tool);
        if (ToolShortcuts.HasVariant(ctx.Tool) && Has(CaptureShortcuts.ToolVariant, input))
            return Route(OverlayCommand.ToggleToolVariant, CaptureShortcuts.ToolVariant);
        if (ctx.Tool == ToolKind.Number)
        {
            if (Has(CaptureShortcuts.NumberUp, input)) return Route(OverlayCommand.NumberUp, CaptureShortcuts.NumberUp);
            if (Has(CaptureShortcuts.NumberDown, input)) return Route(OverlayCommand.NumberDown, CaptureShortcuts.NumberDown);
        }
        if (Has(CaptureShortcuts.PenSizeDown, input)) return Route(OverlayCommand.PenSizeDown, CaptureShortcuts.PenSizeDown);
        if (Has(CaptureShortcuts.PenSizeUp, input)) return Route(OverlayCommand.PenSizeUp, CaptureShortcuts.PenSizeUp);
        if (Has(CaptureShortcuts.NextTool, input)) return Route(OverlayCommand.NextTool, CaptureShortcuts.NextTool);
        if (Has(CaptureShortcuts.PreviousTool, input)) return Route(OverlayCommand.PreviousTool, CaptureShortcuts.PreviousTool);
        if (ctx.HasSelection && Has(CaptureShortcuts.CopyAnnotation, input))
            return Route(OverlayCommand.CopyAnnotation, CaptureShortcuts.CopyAnnotation);
        if (Has(CaptureShortcuts.PasteAnnotation, input)) return Route(OverlayCommand.PasteAnnotation, CaptureShortcuts.PasteAnnotation);
        if (Has(CaptureShortcuts.PickColor, input)) return Route(OverlayCommand.PickColor, CaptureShortcuts.PickColor);
        return OverlayRoute.Nothing;
    }

    /// <summary>The other output keys: Enter runs the configurable Enter action, V copies the
    /// area's size and Ctrl+Alt+C copies the capture as a file.</summary>
    private OverlayRoute DecideOutput(KeyInput input)
    {
        if (Has(CaptureShortcuts.EnterAction, input)) return Route(OverlayCommand.RunEnterAction, CaptureShortcuts.EnterAction);
        if (Has(CaptureShortcuts.CopySize, input)) return Route(OverlayCommand.CopySize, CaptureShortcuts.CopySize);
        if (Has(CaptureShortcuts.CopyAsFile, input)) return Route(OverlayCommand.CopyAsFile, CaptureShortcuts.CopyAsFile);
        return OverlayRoute.Nothing;
    }

    /// <summary>The snip context's keys, in either phase once the button is up: R steps back
    /// through the areas of earlier snips and Shift+R the other way, the backtick or ! shows or
    /// hides the captured pointer, comma and period step through the snip history, and F5
    /// (held: again and again) captures the screen afresh.</summary>
    private OverlayRoute DecideSnip(KeyInput input)
    {
        if (Has(CaptureShortcuts.PreviousRegion, input)) return Route(OverlayCommand.PreviousRegion, CaptureShortcuts.PreviousRegion);
        if (Has(CaptureShortcuts.NextRegion, input)) return Route(OverlayCommand.NextRegion, CaptureShortcuts.NextRegion);
        if (Has(CaptureShortcuts.Refresh, input)) return Route(OverlayCommand.RefreshScreenshot, CaptureShortcuts.Refresh);
        if (Has(CaptureShortcuts.ToggleCursor, input)) return Route(OverlayCommand.ToggleCursor, CaptureShortcuts.ToggleCursor);
        if (Has(CaptureShortcuts.HistoryPrevious, input)) return Route(OverlayCommand.HistoryPrevious, CaptureShortcuts.HistoryPrevious);
        if (Has(CaptureShortcuts.HistoryNext, input)) return Route(OverlayCommand.HistoryNext, CaptureShortcuts.HistoryNext);
        return OverlayRoute.Nothing;
    }

    /// <summary>
    /// The text box sees keys first: Esc and Enter end the edit, Shift+Enter is a new line,
    /// and everything else is typing. A gesture the box then leaves unhandled (Ctrl+S, or
    /// Ctrl+Z with nothing left to undo in the box) still runs, but only by a key that holds
    /// Ctrl, Alt or Win, so a bare-letter binding can never fire mid-word.
    /// </summary>
    private OverlayRoute RouteWhileTyping(OverlayKeyContext ctx, KeyInput input)
    {
        if (ctx.ImeProcessed) return OverlayRoute.Pass;   // a CJK composition owns Enter/Esc

        if (!ctx.TextBoxDeclined)
        {
            if (ctx.Key == Key.Escape) return new(OverlayCommand.CommitText, null);
            if (ctx.Key == Key.Enter && !ctx.Modifiers.HasFlag(ModifierKeys.Shift)) return new(OverlayCommand.CommitText, null);
            return OverlayRoute.Pass;
        }

        if ((ctx.Modifiers & ChordModifiers) == 0) return OverlayRoute.Pass;
        var gesture = DecideGesture(ctx, input, typing: true);
        return gesture.Command == OverlayCommand.None ? OverlayRoute.Pass : gesture;
    }

    /// <summary>Snipaste-style: cancel a framed-source drag, then finish a line strip, then
    /// clear the selection, then leave the capture (confirming when editing), with no
    /// reselect step. Selecting leaves at once. The drag goes first because it owns the mouse
    /// and Deselect hides its handles, so it would otherwise keep resizing invisibly.</summary>
    private static OverlayCommand Escape(OverlayKeyContext ctx)
    {
        if (ctx.Phase == OverlayPhase.Selecting) return OverlayCommand.Close;
        if (ctx.FramedSourceDragging) return OverlayCommand.CancelFramedDrag;
        if (ctx.LineStripActive) return OverlayCommand.FinishLineStrip;
        if (ctx.HasSelection) return OverlayCommand.Deselect;
        return OverlayCommand.ConfirmClose;
    }

    private OverlayRoute DecideGesture(OverlayKeyContext ctx, KeyInput input, bool typing)
    {
        foreach (var (id, command) in Gestures)
        {
            if (command == OverlayCommand.ClearAll && ctx.Dragging) continue;   // a drag owns the annotations
            foreach (var key in _keys.For(id))
                if (key.Matches(input) && (!typing || key.HasCommandModifier)) return Route(command, id);
        }
        return OverlayRoute.Nothing;
    }

    private bool Has(string id, KeyInput input) => _keys.Matches(id, input);

    /// <summary>A key bound as is, or <paramref name="plainKey"/> with any modifiers while it is
    /// bound plain: Esc and Delete always worked with Shift still down from a Shift+drag.</summary>
    private bool HasWithAnyModifiers(string id, Key plainKey, KeyInput input) =>
        Has(id, input) ||
        (input.Key == plainKey && _keys.For(id).Contains(ShortcutKey.Chord(plainKey, ModifierKeys.None)));

    /// <summary>A key bound as is, or, mid-drag, with Shift added (a Shift+drag square can be
    /// fine-tuned with W/A/S/D, and moved with Space).</summary>
    private bool HasWhileDragging(string id, OverlayKeyContext ctx, KeyInput input) =>
        Has(id, input) ||
        (ctx.Dragging && input.Modifiers.HasFlag(ModifierKeys.Shift) &&
         Has(id, input with { Modifiers = input.Modifiers & ~ModifierKeys.Shift }));

    private ShortcutAction? First(IEnumerable<string> ids, KeyInput input)
    {
        foreach (var id in ids)
            if (Has(id, input)) return ShortcutActions.ById(id);
        return null;
    }

    /// <summary>The first of the cursor-nudging <paramref name="ids"/> the key is, Shift allowed mid-drag.</summary>
    private ShortcutAction? FirstWhileDragging(IEnumerable<string> ids, OverlayKeyContext ctx, KeyInput input)
    {
        foreach (var id in ids)
            if (HasWhileDragging(id, ctx, input)) return ShortcutActions.ById(id);
        return null;
    }

    private static OverlayRoute Route(OverlayCommand command, string id) => new(command, ShortcutActions.ById(id));
}
