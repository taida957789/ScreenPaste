using System.Windows;
using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// Keyboard handling for the capture overlay. Which command a key runs is decided by the
/// pure <see cref="OverlayKeyRouter"/> from the user's keys (<see cref="ShortcutMap"/>, read
/// once per capture); this half builds its context from the window state and executes the
/// result, taking a direction or a tool from the action whose key was pressed.
///
/// Keys are taken on the tunnelling Preview* events, so a focused toolbar button, slider or
/// a Tab focus move can never swallow a shortcut. The one control that sees keys first is
/// the text-tool box: the router passes them through, and the bubbling KeyDown picks up the
/// gestures the box leaves unhandled (Ctrl+S while typing). The IME is off for the overlay so
/// letter keys arrive as keys rather than ImeProcessed; the text box turns it back on.
/// </summary>
public partial class CaptureOverlayWindow
{
    private OverlayKeyRouter _keyRouter = null!;
    private ShortcutMap _shortcuts = null!;   // also labels the toolbar's tooltips
    private readonly ModifierTapTracker _modifierTaps = new();
    private bool _altChordRan;   // a key held with Alt ran a command since Alt went down
    private bool _keyRepeating;  // the last key down was an auto-repeat (its typed character too)

    private void InitKeyboard()
    {
        _shortcuts = ShortcutMap.FromSettings(_settings);
        _keyRouter = new OverlayKeyRouter(_shortcuts);

        // Inherited by every element in the overlay; the text box opts back in (PlaceText).
        InputMethod.SetIsInputMethodEnabled(this, false);

        PreviewKeyDown += Overlay_PreviewKeyDown;
        PreviewKeyUp += Overlay_PreviewKeyUp;
        KeyDown += Overlay_KeyDown;
        // Layout-dependent keys ([ ] + - ` ! , .) go by the character they type, which only
        // arrives for keys no key-down handler claimed.
        PreviewTextInput += Overlay_PreviewTextInput;

        // A mouse button, the wheel or losing focus between a modifier's down and up means
        // it was part of a gesture (Shift+drag, Shift+wheel), not a tap.
        PreviewMouseDown += (_, _) => _modifierTaps.Cancel();
        PreviewMouseUp += (_, _) => _modifierTaps.Cancel();
        PreviewMouseWheel += (_, _) => _modifierTaps.Cancel();
        Deactivated += (_, _) => _modifierTaps.Cancel();
    }

    private void Overlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = KeyInputNormalizer.Effective(e);
        _keyRepeating = e.IsRepeat;
        _modifierTaps.KeyDown(key, Keyboard.Modifiers, AnyMouseButtonDown());
        if (IsAltKey(key) && !e.IsRepeat) _altChordRan = false;
        var ctx = KeyContext(key, e);
        if (!RunKeyCommand(_keyRouter.ResolveKey(ctx), ctx)) return;
        e.Handled = true;
        if (ctx.Modifiers.HasFlag(ModifierKeys.Alt)) _altChordRan = true;
    }

    /// <summary>Reached only by keys nobody handled on the way down; that matters solely
    /// while typing, when the router deferred to the text box.</summary>
    private void Overlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (_editingText == null) return;   // everything else was routed on the way down
        var ctx = KeyContext(KeyInputNormalizer.Effective(e), e) with { TextBoxDeclined = true };
        if (RunKeyCommand(_keyRouter.ResolveKey(ctx), ctx)) e.Handled = true;
    }

    private void Overlay_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = KeyInputNormalizer.Effective(e);
        var layoutText = KeyboardLayout.UnshiftedText(key);
        if (_keyRouter.Releases(HoldToMoveKey, key, layoutText)) EndHoldToMove();
        if (_keyRouter.Releases(CaptureShortcuts.Moves, key, layoutText)) CommitAnnotationNudge();   // a held move key's moves: one undo step
        var tapped = _modifierTaps.KeyUp(key);
        if (IsAltKey(key) && _altChordRan)
        {
            // Windows never saw the handled key of an Alt chord (Alt+C), so it would take
            // this release for an Alt tap and enter menu mode, eating the next key.
            _altChordRan = false;
            if (tapped == ModifierKeys.None) e.Handled = true;
        }
        if (tapped == ModifierKeys.None) return;
        var ctx = KeyContext(key, e);
        if (RunKeyCommand(_keyRouter.ResolveTap(tapped, ctx), ctx)) e.Handled = true;
    }

    private void Overlay_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;   // Ctrl chords type control characters
        var ctx = StateContext() with { IsRepeat = _keyRepeating };   // typed by the key just down
        if (RunKeyCommand(_keyRouter.ResolveText(e.Text, ctx), ctx)) e.Handled = true;
    }

    private OverlayKeyContext KeyContext(Key key, KeyEventArgs e) => StateContext() with
    {
        Key = key,
        Text = KeyboardLayout.UnshiftedText(key),
        IsRepeat = e.IsRepeat,
        ImeProcessed = e.Key == Key.ImeProcessed,
    };

    /// <summary>The overlay's state as the router sees it, without a key.</summary>
    private OverlayKeyContext StateContext() => new()
    {
        Phase = _phase == Phase.Editing ? OverlayPhase.Editing : OverlayPhase.Selecting,
        Modifiers = Keyboard.Modifiers,
        TextBoxActive = _editingText != null,
        DropDownOpen = _fontCombo?.IsDropDownOpen == true,   // built with the toolbar
        LoupeVisible = Magnifier.Visibility == Visibility.Visible,
        HasSelection = _selected != null,
        FramedSourceDragging = _magnifySourceDrag != null || _magnifySourceResize != null,
        Dragging = Mouse.LeftButton == MouseButtonState.Pressed,
        HasDetectedArea = _phase == Phase.Selecting && _detectRect != null,
        Tool = _tool,
        LineStripActive = _strip != null,
    };

    private static readonly string[] HoldToMoveKey = { CaptureShortcuts.HoldToMove };

    private static bool IsAltKey(Key key) => key is Key.LeftAlt or Key.RightAlt;

    /// <summary>The commands an open line strip survives: nothing at all, the cursor and
    /// loupe keys, and the line's own width, arrowhead and colour keys and the toolbar toggle,
    /// whose changes the strip then shows.</summary>
    private static bool KeepsLineStrip(OverlayCommand command) => command is
        OverlayCommand.None or OverlayCommand.PassThrough or OverlayCommand.NudgeCursor or
        OverlayCommand.HoldToMove or OverlayCommand.CopyLoupeColor or OverlayCommand.CycleLoupeFormat or
        OverlayCommand.PenSizeDown or OverlayCommand.PenSizeUp or OverlayCommand.ToggleToolVariant or
        OverlayCommand.PickColor or OverlayCommand.ToggleToolbar;

    private static bool AnyMouseButtonDown() =>
        Mouse.LeftButton == MouseButtonState.Pressed ||
        Mouse.RightButton == MouseButtonState.Pressed ||
        Mouse.MiddleButton == MouseButtonState.Pressed;

    /// <summary>Execute a routed command; false leaves the key unhandled. A directional
    /// command reads its direction (and grow or shrink) from the key the action stands for
    /// (<see cref="ShortcutAction.Canonical"/>), whichever key the user bound to it; the
    /// auto-repeat comes from the key really pressed.</summary>
    private bool RunKeyCommand(OverlayRoute route, OverlayKeyContext pressed)
    {
        var command = route.Command;
        var ctx = route.Action?.Canonical is { } c ? pressed with { Key = c.Key, Modifiers = c.Modifiers } : pressed;
        // A held arrow's moves become one undo step before anything else can happen, and an
        // open line strip is finished before any command that could act on the annotations.
        if (command != OverlayCommand.MoveAnnotation) CommitAnnotationNudge();
        if (!KeepsLineStrip(command)) FinishLineStrip();
        switch (command)
        {
            case OverlayCommand.ToggleDetectMode: ToggleDetectMode(); return true;
            case OverlayCommand.CycleScreenArea: CycleScreenArea(); return true;
            case OverlayCommand.NudgeCursor: NudgeCursor(ctx.Key, ctx.IsRepeat); return true;
            case OverlayCommand.AcceptDetected: AcceptDetected(); return true;
            case OverlayCommand.HoldToMove: BeginHoldToMove(); return true;
            case OverlayCommand.ToggleGuideLines: ToggleGuideLines(); return true;
            case OverlayCommand.MoveRegion:
            case OverlayCommand.GrowRegion:
            case OverlayCommand.ShrinkRegion: NudgeRegion(ctx.Key, ctx.Modifiers); return true;
            case OverlayCommand.CommitText: CommitActiveText(discardIfEmpty: true); return true;
            case OverlayCommand.CancelFramedDrag: CancelMagnifySourceDrag(); return true;
            case OverlayCommand.Deselect: Deselect(); return true;
            case OverlayCommand.ConfirmClose: ConfirmCancel(); return true;
            case OverlayCommand.Close: Cancel(); return true;
            case OverlayCommand.CopyLoupeColor: CopyMagColor(); return true;
            case OverlayCommand.CycleLoupeFormat:
                _magFormat = ColorFormat.Next(_magFormat);
                RefreshMagColorText();
                return true;
            case OverlayCommand.DeleteSelected: DeleteSelected(); return true;
            case OverlayCommand.Undo: _history.Undo(); return true;
            case OverlayCommand.Redo: _history.Redo(); return true;
            case OverlayCommand.Copy: DoCopy(); return true;
            case OverlayCommand.Save: DoSave(); return true;
            case OverlayCommand.QuickSave: DoQuickSave(); return true;
            case OverlayCommand.Pin: DoPin(); return true;
            case OverlayCommand.Print: DoPrint(); return true;
            case OverlayCommand.CopyAsFile: DoCopyAsFile(); return true;
            case OverlayCommand.CopySize:
                if (!ctx.IsRepeat) CopySize();   // a held V copies once
                return true;
            // Taken even when the action is off, so Enter never clicks a focused button.
            case OverlayCommand.RunEnterAction: RunOutputAction(EnterAction); return true;
            case OverlayCommand.ToggleToolbar:
                if (!ctx.IsRepeat) ToggleToolbar();   // a held Space must not make it flicker
                return true;
            // The annotation tool keys: CaptureOverlayWindow.Tools.cs; the snip context's
            // (pointer, last areas, history, refresh): CaptureOverlayWindow.History.cs.
            default: return RunToolCommand(route, ctx) || RunSnipCommand(command, ctx);
        }
    }
}
