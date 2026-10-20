using System.Windows;
using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;

namespace ScreenPaste.Capture;

/// <summary>
/// Keyboard handling for the capture overlay. Which command a key runs is decided by the
/// pure <see cref="OverlayKeyRouter"/>; this half builds its context from the window state
/// and executes the result.
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
    private readonly ModifierTapTracker _modifierTaps = new();

    private void InitKeyboard()
    {
        _keyRouter = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(_settings));

        // Inherited by every element in the overlay; the text box opts back in (PlaceText).
        InputMethod.SetIsInputMethodEnabled(this, false);

        PreviewKeyDown += Overlay_PreviewKeyDown;
        PreviewKeyUp += Overlay_PreviewKeyUp;
        KeyDown += Overlay_KeyDown;

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
        _modifierTaps.KeyDown(key, Keyboard.Modifiers, AnyMouseButtonDown());
        if (RunKeyCommand(_keyRouter.Route(KeyContext(key, e)))) e.Handled = true;
    }

    /// <summary>Reached only by keys nobody handled on the way down; that matters solely
    /// while typing, when the router deferred to the text box.</summary>
    private void Overlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (_editingText == null) return;   // everything else was routed on the way down
        var ctx = KeyContext(KeyInputNormalizer.Effective(e), e) with { TextBoxDeclined = true };
        if (RunKeyCommand(_keyRouter.Route(ctx))) e.Handled = true;
    }

    private void Overlay_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = KeyInputNormalizer.Effective(e);
        var tapped = _modifierTaps.KeyUp(key);
        if (tapped == ModifierKeys.None) return;
        if (RunKeyCommand(_keyRouter.RouteTap(tapped, KeyContext(key, e)))) e.Handled = true;
    }

    private OverlayKeyContext KeyContext(Key key, KeyEventArgs e) => new()
    {
        Phase = _phase == Phase.Editing ? OverlayPhase.Editing : OverlayPhase.Selecting,
        Key = key,
        Modifiers = Keyboard.Modifiers,
        ImeProcessed = e.Key == Key.ImeProcessed,
        TextBoxActive = _editingText != null,
        DropDownOpen = _fontCombo?.IsDropDownOpen == true,   // built with the toolbar
        LoupeVisible = Magnifier.Visibility == Visibility.Visible,
        HasSelection = _selected != null,
        FramedSourceDragging = _magnifySourceDrag != null || _magnifySourceResize != null,
    };

    private static bool AnyMouseButtonDown() =>
        Mouse.LeftButton == MouseButtonState.Pressed ||
        Mouse.RightButton == MouseButtonState.Pressed ||
        Mouse.MiddleButton == MouseButtonState.Pressed;

    /// <summary>Execute a routed command; false leaves the key unhandled.</summary>
    private bool RunKeyCommand(OverlayCommand command)
    {
        switch (command)
        {
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
            default: return false;   // None, PassThrough, and rows not wired up yet
        }
    }
}
