using System.Windows.Input;
using ScreenPaste.Capture;
using ScreenPaste.Input;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The capture overlay's key table: which command a key press runs in each phase and
/// state. The window only builds the context and executes the result, so the precedence
/// rules (text box first, the Esc chain, QuickSave before Save ...) live and are tested here.
/// </summary>
public sealed class OverlayKeyRouterTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    private static readonly OverlayKeyRouter Router = new(OverlayKeyBindings.FromSettings(new AppSettings()));

    private static OverlayKeyContext Selecting(Key key, ModifierKeys modifiers = None) => new()
    {
        Phase = OverlayPhase.Selecting,
        Key = key,
        Modifiers = modifiers,
        LoupeVisible = true,
    };

    private static OverlayKeyContext Editing(Key key, ModifierKeys modifiers = None) =>
        Selecting(key, modifiers) with { Phase = OverlayPhase.Editing };

    private static OverlayKeyContext Typing(Key key, ModifierKeys modifiers = None) =>
        Editing(key, modifiers) with { TextBoxActive = true };

    // ---------------------------------------------------------------- Esc chain ---

    [Fact]
    public void Esc_WhileSelecting_ClosesTheOverlay()
    {
        Assert.Equal(OverlayCommand.Close, Router.Route(Selecting(Key.Escape)));
    }

    [Fact]
    public void Esc_WhileEditing_CancelsAFramedSourceDragFirst()
    {
        var ctx = Editing(Key.Escape) with { FramedSourceDragging = true, HasSelection = true };

        Assert.Equal(OverlayCommand.CancelFramedDrag, Router.Route(ctx));
    }

    [Fact]
    public void Esc_WhileEditing_ThenDeselectsTheAnnotation()
    {
        var ctx = Editing(Key.Escape) with { HasSelection = true };

        Assert.Equal(OverlayCommand.Deselect, Router.Route(ctx));
    }

    [Fact]
    public void Esc_WhileEditing_WithNothingSelected_AsksBeforeClosing()
    {
        Assert.Equal(OverlayCommand.ConfirmClose, Router.Route(Editing(Key.Escape)));
    }

    // ---------------------------------------------------------------- text box ---

    [Theory]
    [InlineData(Key.Escape, None)]
    [InlineData(Key.Enter, None)]
    [InlineData(Key.Enter, Ctrl)]
    public void EscOrEnter_WhileTyping_CommitsTheText(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.CommitText, Router.Route(Typing(key, modifiers)));
    }

    [Theory]
    [InlineData(Key.Enter, Shift)]   // a new line in the box
    [InlineData(Key.C, None)]        // typing, not the loupe's copy
    [InlineData(Key.Z, Ctrl)]        // the box's own undo goes first
    [InlineData(Key.S, Ctrl)]
    [InlineData(Key.Delete, None)]
    [InlineData(Key.Left, None)]
    public void OtherKeys_WhileTyping_PassThroughToTheTextBox(Key key, ModifierKeys modifiers)
    {
        var ctx = Typing(key, modifiers) with { HasSelection = true };

        Assert.Equal(OverlayCommand.PassThrough, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public void ImeProcessedKeys_WhileTyping_BelongToTheIme(Key key)
    {
        // Enter confirms and Esc cancels a CJK composition; neither may end the text edit.
        var ctx = Typing(key) with { ImeProcessed = true };

        Assert.Equal(OverlayCommand.PassThrough, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Key.S, Ctrl, OverlayCommand.Save)]
    [InlineData(Key.S, Ctrl | Shift, OverlayCommand.QuickSave)]
    [InlineData(Key.Z, Ctrl, OverlayCommand.Undo)]
    [InlineData(Key.C, Ctrl, OverlayCommand.Copy)]
    public void Gestures_TheTextBoxLeftUnhandled_StillRun(Key key, ModifierKeys modifiers, OverlayCommand expected)
    {
        var ctx = Typing(key, modifiers) with { TextBoxDeclined = true };

        Assert.Equal(expected, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Key.C)]
    [InlineData(Key.Delete)]
    public void UnmodifiedKeys_TheTextBoxLeftUnhandled_AreStillTyping(Key key)
    {
        var ctx = Typing(key) with { TextBoxDeclined = true, HasSelection = true };

        Assert.Equal(OverlayCommand.PassThrough, Router.Route(ctx));
    }

    [Fact]
    public void ABareLetterGesture_NeverFiresWhileTyping()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Copy = new KeyChord(Key.C, None) };
        var ctx = Typing(Key.C) with { TextBoxDeclined = true };

        Assert.Equal(OverlayCommand.PassThrough, new OverlayKeyRouter(bindings).Route(ctx));
    }

    [Fact]
    public void Keys_WhileTheFontListIsOpen_BelongToTheList()
    {
        var ctx = Editing(Key.Escape) with { DropDownOpen = true };

        Assert.Equal(OverlayCommand.PassThrough, Router.Route(ctx));
    }

    // ---------------------------------------------------------------- loupe ---

    [Fact]
    public void C_WithTheLoupeVisible_CopiesItsColour_InBothPhases()
    {
        Assert.Equal(OverlayCommand.CopyLoupeColor, Router.Route(Selecting(Key.C)));
        Assert.Equal(OverlayCommand.CopyLoupeColor, Router.Route(Editing(Key.C)));
    }

    [Fact]
    public void C_WithTheLoupeHidden_DoesNothing()
    {
        var ctx = Selecting(Key.C) with { LoupeVisible = false };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Shift)]
    [InlineData(Alt)]
    public void C_WithAModifier_IsNotTheLoupeCopy(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.C, modifiers)));
    }

    [Fact]
    public void CtrlC_WhileEditing_CopiesTheCapture_NotTheLoupeColour()
    {
        Assert.Equal(OverlayCommand.Copy, Router.Route(Editing(Key.C, Ctrl)));
    }

    [Fact]
    public void AShiftTap_WithTheLoupeVisible_CyclesTheColourFormat()
    {
        Assert.Equal(OverlayCommand.CycleLoupeFormat, Router.RouteTap(Shift, Selecting(Key.LeftShift)));
        Assert.Equal(OverlayCommand.CycleLoupeFormat, Router.RouteTap(Shift, Editing(Key.RightShift)));
    }

    [Fact]
    public void AShiftTap_WithTheLoupeHiddenOrWhileTyping_DoesNothing()
    {
        var hidden = Selecting(Key.LeftShift) with { LoupeVisible = false };

        Assert.Equal(OverlayCommand.None, Router.RouteTap(Shift, hidden));
        Assert.Equal(OverlayCommand.None, Router.RouteTap(Shift, Typing(Key.LeftShift)));
    }

    [Fact]
    public void AnAltTap_IsNotTheColourFormatToggle()
    {
        Assert.Equal(OverlayCommand.None, Router.RouteTap(Alt, Selecting(Key.LeftAlt)));
    }

    // ---------------------------------------------------------------- editing keys ---

    [Fact]
    public void Delete_WithAnAnnotationSelected_DeletesIt()
    {
        var ctx = Editing(Key.Delete) with { HasSelection = true };

        Assert.Equal(OverlayCommand.DeleteSelected, Router.Route(ctx));
    }

    [Fact]
    public void Delete_WithNothingSelected_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Delete)));
    }

    [Theory]
    [InlineData(Key.Z, Ctrl, OverlayCommand.Undo)]
    [InlineData(Key.Y, Ctrl, OverlayCommand.Redo)]
    [InlineData(Key.C, Ctrl, OverlayCommand.Copy)]
    [InlineData(Key.S, Ctrl, OverlayCommand.Save)]
    [InlineData(Key.S, Ctrl | Shift, OverlayCommand.QuickSave)]
    public void DefaultGestures_WhileEditing_RunTheirCommand(Key key, ModifierKeys modifiers, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.Route(Editing(key, modifiers)));
    }

    [Fact]
    public void CtrlShiftZ_NeverMatchesUndo()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Z, Ctrl | Shift)));
    }

    [Fact]
    public void QuickSave_TakesPrecedenceOverSave_WhenBothShareAChord()
    {
        var same = new KeyChord(Key.S, Ctrl);
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Save = same, QuickSave = same };

        Assert.Equal(OverlayCommand.QuickSave, new OverlayKeyRouter(bindings).Route(Editing(Key.S, Ctrl)));
    }

    [Theory]
    [InlineData(Key.Z, Ctrl)]
    [InlineData(Key.C, Ctrl)]
    [InlineData(Key.S, Ctrl)]
    [InlineData(Key.Delete, None)]
    public void EditingKeys_WhileSelecting_DoNothing(Key key, ModifierKeys modifiers)
    {
        var ctx = Selecting(key, modifiers) with { HasSelection = true };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    // ---------------------------------------------------------------- bindings ---

    [Fact]
    public void FromSettings_FollowsRebindingAndDisabling()
    {
        var settings = new AppSettings { UndoHotkey = "Ctrl+Alt+Z", RedoHotkey = "" };
        var router = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(settings));

        Assert.Equal(OverlayCommand.Undo, router.Route(Editing(Key.Z, Ctrl | Alt)));
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.Z, Ctrl)));
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.Y, Ctrl)));
    }

    [Fact]
    public void FromSettings_LeavesABareLetterGestureUnbound()
    {
        // A hand-edited settings.json; the Settings window cannot write these.
        var settings = new AppSettings { CopyHotkey = "C", UndoHotkey = "Shift+Z" };
        var bindings = OverlayKeyBindings.FromSettings(settings);
        var router = new OverlayKeyRouter(bindings);

        Assert.Null(bindings.Copy);
        Assert.Null(bindings.Undo);
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.C) with { LoupeVisible = false }));
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.Z, Shift)));
    }

    [Fact]
    public void FromSettings_ReadsTheDefaultGestures()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings());

        Assert.Equal(new KeyChord(Key.Z, Ctrl), bindings.Undo);
        Assert.Equal(new KeyChord(Key.Y, Ctrl), bindings.Redo);
        Assert.Equal(new KeyChord(Key.C, Ctrl), bindings.Copy);
        Assert.Equal(new KeyChord(Key.S, Ctrl), bindings.Save);
        Assert.Equal(new KeyChord(Key.S, Ctrl | Shift), bindings.QuickSave);
    }

    [Fact]
    public void AnUnboundKey_IsLeftAlone()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.F7)));
    }
}
