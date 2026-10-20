using System.Windows.Input;
using ScreenPaste.Capture;
using ScreenPaste.Editor;
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
    [InlineData(Key.T, Ctrl, OverlayCommand.Pin)]
    [InlineData(Key.P, Ctrl, OverlayCommand.Print)]
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
        Assert.NotEqual(OverlayCommand.CycleLoupeFormat, Router.RouteTap(Alt, Selecting(Key.LeftAlt)));
        Assert.Equal(OverlayCommand.None, Router.RouteTap(Alt, Editing(Key.LeftAlt)));
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
    [InlineData(Key.T, Ctrl, OverlayCommand.Pin)]
    [InlineData(Key.P, Ctrl, OverlayCommand.Print)]
    public void DefaultGestures_WhileEditing_RunTheirCommand(Key key, ModifierKeys modifiers, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.Route(Editing(key, modifiers)));
    }

    [Fact]
    public void CtrlShiftZ_ClearsEverything_NeverUndoes()
    {
        Assert.Equal(OverlayCommand.ClearAll, Router.Route(Editing(Key.Z, Ctrl | Shift)));
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

    // ---------------------------------------------------------------- selecting phase ---

    [Fact]
    public void Tab_WhileSelecting_TogglesTheDetectionMode()
    {
        Assert.Equal(OverlayCommand.ToggleDetectMode, Router.Route(Selecting(Key.Tab)));
    }

    [Fact]
    public void Tab_MidDrag_DoesNothing()
    {
        var ctx = Selecting(Key.Tab) with { Dragging = true };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    [Fact]
    public void Tab_WhileEditing_IsNotTheDetectionToggle()
    {
        Assert.NotEqual(OverlayCommand.ToggleDetectMode, Router.Route(Editing(Key.Tab)));
    }

    [Fact]
    public void CtrlA_CyclesTheScreenArea_InBothPhases()
    {
        Assert.Equal(OverlayCommand.CycleScreenArea, Router.Route(Selecting(Key.A, Ctrl)));
        Assert.Equal(OverlayCommand.CycleScreenArea, Router.Route(Editing(Key.A, Ctrl)));
    }

    [Fact]
    public void CtrlA_MidDrag_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.A, Ctrl) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.A, Ctrl) with { Dragging = true }));
    }

    [Fact]
    public void CtrlA_WhileTyping_SelectsTheText()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.A, Ctrl)));
    }

    [Fact]
    public void AGestureBoundToCtrlA_WinsOverTheScreenCycle_WhileEditing()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Copy = new KeyChord(Key.A, Ctrl) };

        Assert.Equal(OverlayCommand.Copy, new OverlayKeyRouter(bindings).Route(Editing(Key.A, Ctrl)));
    }

    [Theory]
    [InlineData(Key.W)]
    [InlineData(Key.A)]
    [InlineData(Key.S)]
    [InlineData(Key.D)]
    public void Wasd_NudgesTheCursor_InBothPhases(Key key)
    {
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Selecting(key)));
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Editing(key)));
    }

    [Theory]
    [InlineData(Key.W)]
    [InlineData(Key.D)]
    public void Wasd_WithTheLeftButtonHeld_NudgesTheDrag(Key key)
    {
        // Snipaste's LMB+WASD: the drag follows the cursor, a pixel at a time.
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Selecting(key) with { Dragging = true }));
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Editing(key) with { Dragging = true }));
    }

    [Fact]
    public void ShiftWasd_NudgesOnlyMidDrag()
    {
        // A Shift+drag square can be fine-tuned; on its own Shift+W is not a nudge.
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Selecting(Key.W, Shift) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.W, Shift)));
    }

    [Theory]
    [InlineData(Key.W, Ctrl)]
    [InlineData(Key.D, Alt)]
    public void ChordedWasd_IsNotANudge(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(key, modifiers)));
        Assert.NotEqual(OverlayCommand.NudgeCursor, Router.Route(Editing(key, modifiers)));
    }

    [Fact]
    public void CtrlS_WhileEditing_StillSaves_NotNudges()
    {
        Assert.Equal(OverlayCommand.Save, Router.Route(Editing(Key.S, Ctrl)));
    }

    [Theory]
    [InlineData(Key.W)]
    [InlineData(Key.S)]
    public void Wasd_WhileTyping_IsTyping(Key key)
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(key)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(key) with { TextBoxDeclined = true }));
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void Arrows_WhileSelecting_NudgeTheCursor(Key key)
    {
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Selecting(key)));
        Assert.Equal(OverlayCommand.NudgeCursor, Router.Route(Selecting(key) with { Dragging = true }));
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Left)]
    public void Arrows_WhileEditing_AreNotTheCursorNudge(Key key)
    {
        // Once a region is chosen the arrows move it (or the selected annotation) instead.
        Assert.NotEqual(OverlayCommand.NudgeCursor, Router.Route(Editing(key)));
    }

    // ---------------------------------------------------------------- region keys ---

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void Arrows_WhileEditing_MoveTheRegion(Key key)
    {
        Assert.Equal(OverlayCommand.MoveRegion, Router.Route(Editing(key)));
        Assert.Equal(OverlayCommand.MoveRegion, Router.Route(Editing(key) with { IsRepeat = true }));
    }

    [Fact]
    public void Arrows_WithAnAnnotationSelected_LeaveTheRegionAlone()
    {
        // The selected annotation owns the plain arrows.
        var ctx = Editing(Key.Left) with { HasSelection = true };

        Assert.NotEqual(OverlayCommand.MoveRegion, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Right)]
    public void CtrlArrows_WhileEditing_GrowTheRegion(Key key)
    {
        Assert.Equal(OverlayCommand.GrowRegion, Router.Route(Editing(key, Ctrl)));
        Assert.Equal(OverlayCommand.GrowRegion, Router.Route(Editing(key, Ctrl) with { HasSelection = true }));
    }

    [Theory]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    public void ShiftArrows_WhileEditing_ShrinkTheRegion(Key key)
    {
        Assert.Equal(OverlayCommand.ShrinkRegion, Router.Route(Editing(key, Shift)));
        Assert.Equal(OverlayCommand.ShrinkRegion, Router.Route(Editing(key, Shift) with { HasSelection = true }));
    }

    [Theory]
    [InlineData(None)]
    [InlineData(Ctrl)]
    [InlineData(Shift)]
    public void RegionArrows_MidDrag_DoNothing(ModifierKeys modifiers)
    {
        // A handle resize or a drawing owns the region until the button is up.
        var ctx = Editing(Key.Up, modifiers) with { Dragging = true };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Ctrl | Shift)]
    [InlineData(Alt)]
    public void OtherChordedArrows_WhileEditing_DoNothing(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Right, modifiers)));
    }

    [Theory]
    [InlineData(Key.Up, Ctrl)]
    [InlineData(Key.Down, Shift)]
    public void ChordedArrows_WhileSelecting_DoNotResizeAnything(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(key, modifiers)));
    }

    [Theory]
    [InlineData(Key.Up, None)]
    [InlineData(Key.Left, Ctrl)]
    [InlineData(Key.Right, Shift)]
    public void Arrows_WhileTyping_MoveTheCaret(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(key, modifiers)));
    }

    [Fact]
    public void AGestureBoundToCtrlArrow_WinsOverTheRegionGrow()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Save = new KeyChord(Key.Right, Ctrl) };

        Assert.Equal(OverlayCommand.Save, new OverlayKeyRouter(bindings).Route(Editing(Key.Right, Ctrl)));
    }

    [Fact]
    public void Space_WhileSelecting_AcceptsTheDetectedArea()
    {
        var ctx = Selecting(Key.Space) with { HasDetectedArea = true };

        Assert.Equal(OverlayCommand.AcceptDetected, Router.Route(ctx));
    }

    [Fact]
    public void Space_WhileSelecting_WithNothingDetected_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.Space)));
    }

    [Fact]
    public void Space_MidDrag_HoldsTheAreaToMoveIt_InBothPhases()
    {
        var selecting = Selecting(Key.Space) with { Dragging = true, HasDetectedArea = true };
        var squareDrag = Selecting(Key.Space, Shift) with { Dragging = true };
        var resizing = Editing(Key.Space) with { Dragging = true };

        Assert.Equal(OverlayCommand.HoldToMove, Router.Route(selecting));
        Assert.Equal(OverlayCommand.HoldToMove, Router.Route(squareDrag));
        Assert.Equal(OverlayCommand.HoldToMove, Router.Route(resizing));
    }

    [Fact]
    public void Space_WhileTyping_IsASpace()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.Space) with { Dragging = true }));
    }

    [Fact]
    public void AnAltTap_WhileSelecting_TogglesTheGuideLines()
    {
        Assert.Equal(OverlayCommand.ToggleGuideLines, Router.RouteTap(Alt, Selecting(Key.LeftAlt)));
        Assert.Equal(OverlayCommand.ToggleGuideLines,
            Router.RouteTap(Alt, Selecting(Key.RightAlt) with { LoupeVisible = false }));
    }

    [Fact]
    public void ACtrlTap_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.RouteTap(Ctrl, Selecting(Key.LeftCtrl)));
    }

    // ---------------------------------------------------------------- outputs ---

    [Fact]
    public void Enter_WhileEditing_RunsTheEnterAction()
    {
        Assert.Equal(OverlayCommand.RunEnterAction, Router.Route(Editing(Key.Enter)));
        Assert.Equal(OverlayCommand.RunEnterAction, Router.Route(Editing(Key.Enter) with { HasSelection = true }));
    }

    [Fact]
    public void Enter_WhileTyping_CommitsTheText_NeverTheEnterAction()
    {
        Assert.Equal(OverlayCommand.CommitText, Router.Route(Typing(Key.Enter)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.Enter) with { TextBoxDeclined = true }));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.Enter) with { ImeProcessed = true }));
    }

    [Theory]
    [InlineData(Shift)]
    [InlineData(Ctrl)]
    [InlineData(Alt)]
    public void ChordedEnter_WhileEditing_IsNotTheEnterAction(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Enter, modifiers)));
    }

    [Fact]
    public void Enter_MidDragOrWhileSelecting_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Enter) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.Enter)));
    }

    [Fact]
    public void Enter_WithTheFontListOpen_PicksTheFont()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Editing(Key.Enter) with { DropDownOpen = true }));
    }

    [Fact]
    public void CtrlInsert_WhileEditing_Copies()
    {
        Assert.Equal(OverlayCommand.Copy, Router.Route(Editing(Key.Insert, Ctrl)));
    }

    [Fact]
    public void CtrlInsert_StillCopies_WhenTheCopyGestureIsRebound()
    {
        // A fixed alias, as in Snipaste: it does not follow CopyHotkey.
        var router = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(new AppSettings { CopyHotkey = "" }));

        Assert.Equal(OverlayCommand.Copy, router.Route(Editing(Key.Insert, Ctrl)));
    }

    [Fact]
    public void Insert_WithoutCtrl_OrWhileSelecting_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Insert)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Insert, Shift)));
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.Insert, Ctrl)));
    }

    [Fact]
    public void CtrlAltC_WhileEditing_CopiesAsAFile()
    {
        Assert.Equal(OverlayCommand.CopyAsFile, Router.Route(Editing(Key.C, Ctrl | Alt)));
    }

    [Fact]
    public void CtrlAltC_WhileSelectingOrTyping_DoesNotCopyAFile()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.C, Ctrl | Alt)));
        // AltGr is Ctrl+Alt: on some layouts it types a letter, so it stays the box's.
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.C, Ctrl | Alt) with { TextBoxDeclined = true }));
    }

    [Fact]
    public void V_WhileEditing_CopiesTheSize()
    {
        Assert.Equal(OverlayCommand.CopySize, Router.Route(Editing(Key.V)));
        Assert.Equal(OverlayCommand.CopySize, Router.Route(Editing(Key.V) with { LoupeVisible = false }));
    }

    [Fact]
    public void V_WhileSelectingTypingOrDragging_IsNotTheSizeCopy()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.V)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.V)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.V) with { Dragging = true }));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(Shift)]
    public void ChordedV_IsNotTheSizeCopy(ModifierKeys modifiers)
    {
        Assert.NotEqual(OverlayCommand.CopySize, Router.Route(Editing(Key.V, modifiers)));
    }

    [Fact]
    public void Space_WhileEditing_TogglesTheToolbar()
    {
        Assert.Equal(OverlayCommand.ToggleToolbar, Router.Route(Editing(Key.Space)));
        Assert.Equal(OverlayCommand.ToggleToolbar, Router.Route(Editing(Key.Space) with { HasSelection = true }));
        Assert.Equal(OverlayCommand.ToggleToolbar, Router.Route(Editing(Key.Space) with { IsRepeat = true }));
    }

    [Fact]
    public void Space_MidDrag_IsNotTheToolbarToggle()
    {
        // The held button owns Space: it moves the area instead.
        Assert.Equal(OverlayCommand.HoldToMove, Router.Route(Editing(Key.Space) with { Dragging = true }));
    }

    [Fact]
    public void Space_WhileSelectingOrTyping_IsNotTheToolbarToggle()
    {
        Assert.NotEqual(OverlayCommand.ToggleToolbar, Router.Route(Selecting(Key.Space)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.Space)));
    }

    [Theory]
    [InlineData(Shift)]
    [InlineData(Ctrl)]
    public void ChordedSpace_WhileEditing_DoesNothing(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Space, modifiers)));
    }

    [Fact]
    public void AGestureBoundToCtrlInsert_WinsOverTheCopyAlias()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Pin = new KeyChord(Key.Insert, Ctrl) };

        Assert.Equal(OverlayCommand.Pin, new OverlayKeyRouter(bindings).Route(Editing(Key.Insert, Ctrl)));
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
        Assert.Equal(new KeyChord(Key.T, Ctrl), bindings.Pin);
        Assert.Equal(new KeyChord(Key.P, Ctrl), bindings.Print);
        Assert.Equal(new KeyChord(Key.Z, Ctrl | Shift), bindings.ClearAll);
    }

    [Fact]
    public void FromSettings_FollowsThePinAndPrintGestures()
    {
        var settings = new AppSettings { PinHotkey = "Ctrl+Shift+T", PrintHotkey = "" };
        var router = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(settings));

        Assert.Equal(OverlayCommand.Pin, router.Route(Editing(Key.T, Ctrl | Shift)));
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.T, Ctrl)));
        Assert.Equal(OverlayCommand.None, router.Route(Editing(Key.P, Ctrl)));
    }

    [Fact]
    public void AnUnboundKey_IsLeftAlone()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.F7)));
    }

    // ---------------------------------------------------------------- clear all ---

    [Fact]
    public void CtrlShiftZ_MidDrag_DoesNotClear()
    {
        // A drawing or a move owns the annotations until the button is up.
        var ctx = Editing(Key.Z, Ctrl | Shift) with { Dragging = true };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    [Fact]
    public void CtrlShiftZ_WhileSelecting_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.Z, Ctrl | Shift)));
    }

    [Fact]
    public void ARedoBoundToCtrlShiftZ_WinsOverClearAll()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Redo = new KeyChord(Key.Z, Ctrl | Shift) };

        Assert.Equal(OverlayCommand.Redo, new OverlayKeyRouter(bindings).Route(Editing(Key.Z, Ctrl | Shift)));
    }

    [Fact]
    public void FromSettings_FollowsTheClearAllGesture()
    {
        var rebound = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(new AppSettings { ClearAllHotkey = "Ctrl+Shift+Delete" }));
        var disabled = new OverlayKeyRouter(OverlayKeyBindings.FromSettings(new AppSettings { ClearAllHotkey = "" }));

        Assert.Equal(OverlayCommand.ClearAll, rebound.Route(Editing(Key.Delete, Ctrl | Shift)));
        Assert.Equal(OverlayCommand.None, rebound.Route(Editing(Key.Z, Ctrl | Shift)));
        Assert.Equal(OverlayCommand.None, disabled.Route(Editing(Key.Z, Ctrl | Shift)));
    }

    // ---------------------------------------------------------------- annotation arrows ---

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void Arrows_WithAnAnnotationSelected_MoveTheAnnotation(Key key)
    {
        var ctx = Editing(key) with { HasSelection = true };

        Assert.Equal(OverlayCommand.MoveAnnotation, Router.Route(ctx));
        Assert.Equal(OverlayCommand.MoveAnnotation, Router.Route(ctx with { IsRepeat = true }));
    }

    [Fact]
    public void Arrows_WithAnAnnotationSelected_MidDrag_DoNothing()
    {
        var ctx = Editing(Key.Left) with { HasSelection = true, Dragging = true };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    // ---------------------------------------------------------------- tool letters ---

    [Theory]
    [InlineData(Key.T)]
    [InlineData(Key.B)]
    [InlineData(Key.M)]
    [InlineData(Key.E)]
    public void ToolLetters_WhileEditing_SwitchTheTool(Key key)
    {
        Assert.Equal(OverlayCommand.SelectToolByLetter, Router.Route(Editing(key)));
    }

    [Theory]
    [InlineData(Shift)]
    [InlineData(Alt)]
    public void ChordedToolLetters_AreNotToolSwitches(ModifierKeys modifiers)
    {
        Assert.NotEqual(OverlayCommand.SelectToolByLetter, Router.Route(Editing(Key.B, modifiers)));
    }

    [Fact]
    public void ToolLetters_WhileSelectingTypingOrDragging_DoNotSwitchTools()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.T)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.T)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.T) with { Dragging = true }));
    }

    [Fact]
    public void AnUnmappedLetter_WhileEditing_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.J)));
    }

    // ---------------------------------------------------------------- digits ---

    [Theory]
    [InlineData(Key.D0)]
    [InlineData(Key.D1)]
    [InlineData(Key.D2)]
    [InlineData(Key.D9)]
    [InlineData(Key.NumPad3)]
    public void CtrlDigits_SwitchTheTool(Key key)
    {
        Assert.Equal(OverlayCommand.SelectToolByNumber, Router.Route(Editing(key, Ctrl)));
    }

    [Theory]
    [InlineData(Key.D1, OverlayCommand.PenSizeDown)]
    [InlineData(Key.NumPad1, OverlayCommand.PenSizeDown)]
    [InlineData(Key.D2, OverlayCommand.PenSizeUp)]
    [InlineData(Key.NumPad2, OverlayCommand.PenSizeUp)]
    public void Digits1And2_WithoutCtrl_StepThePenSize(Key key, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.Route(Editing(key)));
    }

    [Theory]
    [InlineData(Key.D3)]
    [InlineData(Key.D0)]
    public void OtherDigits_WithoutCtrl_DoNothing(Key key)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(key)));
    }

    [Theory]
    [InlineData(Key.D1, None)]
    [InlineData(Key.D1, Ctrl)]
    public void Digits_WhileSelecting_DoNothing(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(key, modifiers)));
    }

    [Fact]
    public void Digits_MidDrag_DoNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.D2) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.D2, Ctrl) with { Dragging = true }));
    }

    [Fact]
    public void Digits_WhileTyping_AreTyping()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.D1)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.D1, Ctrl)));
    }

    [Fact]
    public void AGestureBoundToCtrlDigit_WinsOverTheToolSwitch()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Save = new KeyChord(Key.D1, Ctrl) };

        Assert.Equal(OverlayCommand.Save, new OverlayKeyRouter(bindings).Route(Editing(Key.D1, Ctrl)));
    }

    // ---------------------------------------------------------------- Tab / Ctrl+Tab ---

    [Theory]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.Line)]
    [InlineData(ToolKind.Blur)]
    [InlineData(ToolKind.Magnify)]
    public void Tab_WithAToolThatHasVariants_TogglesTheVariant(ToolKind tool)
    {
        var ctx = Editing(Key.Tab) with { Tool = tool };

        Assert.Equal(OverlayCommand.ToggleToolVariant, Router.Route(ctx));
    }

    [Theory]
    [InlineData(ToolKind.None)]
    [InlineData(ToolKind.MarkerPen)]
    [InlineData(ToolKind.Text)]
    public void Tab_WithAToolWithoutVariants_IsLeftAlone(ToolKind tool)
    {
        var ctx = Editing(Key.Tab) with { Tool = tool };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    [Fact]
    public void CtrlTab_CyclesToTheNextTool_AndCtrlShiftTab_ToThePrevious()
    {
        Assert.Equal(OverlayCommand.NextTool, Router.Route(Editing(Key.Tab, Ctrl)));
        Assert.Equal(OverlayCommand.PreviousTool, Router.Route(Editing(Key.Tab, Ctrl | Shift)));
    }

    [Fact]
    public void CtrlTab_WhileSelectingOrMidDrag_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.Tab, Ctrl)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.Tab, Ctrl) with { Dragging = true }));
    }

    [Fact]
    public void ShiftTab_IsNotAVariantToggle()
    {
        var ctx = Editing(Key.Tab, Shift) with { Tool = ToolKind.Shape };

        Assert.Equal(OverlayCommand.None, Router.Route(ctx));
    }

    // ---------------------------------------------------------------- Alt+C ---

    [Fact]
    public void AltC_WhileEditing_PicksTheScreenColour()
    {
        Assert.Equal(OverlayCommand.PickColor, Router.Route(Editing(Key.C, Alt)));
    }

    [Fact]
    public void AltC_WhileSelectingOrMidDrag_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.C, Alt)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.C, Alt) with { Dragging = true }));
    }

    [Fact]
    public void CtrlAltC_IsStillCopyAsFile_NotTheColourPick()
    {
        Assert.Equal(OverlayCommand.CopyAsFile, Router.Route(Editing(Key.C, Ctrl | Alt)));
    }

    // ---------------------------------------------------------------- typed characters ---

    [Theory]
    [InlineData("[", OverlayCommand.PenSizeDown)]
    [InlineData("]", OverlayCommand.PenSizeUp)]
    public void Brackets_WhileEditing_StepThePenSize(string text, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.RouteText(text, Editing(Key.None)));
    }

    [Theory]
    [InlineData(Ctrl | Alt)]   // AltGr: how a German or French keyboard types a bracket
    [InlineData(Shift)]
    public void Brackets_MatchByCharacter_WhateverModifiersTypedThem(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.PenSizeUp, Router.RouteText("]", Editing(Key.None, modifiers)));
    }

    [Fact]
    public void Brackets_WhileTypingOrInTheFontList_AreLeftToTheControl()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.RouteText("[", Typing(Key.None)));
        Assert.Equal(OverlayCommand.PassThrough, Router.RouteText("[", Editing(Key.None) with { DropDownOpen = true }));
    }

    [Fact]
    public void Brackets_WhileSelectingOrMidDrag_DoNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.RouteText("[", Selecting(Key.None)));
        Assert.Equal(OverlayCommand.None, Router.RouteText("]", Editing(Key.None) with { Dragging = true }));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("1")]
    [InlineData("")]
    public void OtherText_IsNotAShortcut(string text)
    {
        Assert.Equal(OverlayCommand.None, Router.RouteText(text, Editing(Key.None)));
    }

    // ---------------------------------------------------------------- number marker ---

    private static OverlayKeyContext Numbering() => Editing(Key.None) with { Tool = ToolKind.Number };

    [Theory]
    [InlineData("+", OverlayCommand.NumberUp)]
    [InlineData("=", OverlayCommand.NumberUp)]
    [InlineData("-", OverlayCommand.NumberDown)]
    public void PlusAndMinus_WithTheNumberTool_ChangeTheNextNumber(string text, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.RouteText(text, Numbering()));
    }

    [Theory]
    [InlineData(Shift)]       // + over = on a US keyboard
    [InlineData(Ctrl | Alt)]  // AltGr
    public void PlusAndMinus_MatchByCharacter_WhateverModifiersTypedThem(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.NumberUp, Router.RouteText("+", Numbering() with { Modifiers = modifiers }));
    }

    [Theory]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.None)]
    public void PlusAndMinus_WithAnotherTool_DoNothing(ToolKind tool)
    {
        Assert.Equal(OverlayCommand.None, Router.RouteText("+", Editing(Key.None) with { Tool = tool }));
    }

    [Fact]
    public void PlusAndMinus_WhileTypingSelectingOrMidDrag_AreNotTheNumberKeys()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.RouteText("+", Numbering() with { TextBoxActive = true }));
        Assert.Equal(OverlayCommand.None, Router.RouteText("+", Numbering() with { Phase = OverlayPhase.Selecting }));
        Assert.Equal(OverlayCommand.None, Router.RouteText("-", Numbering() with { Dragging = true }));
    }

    [Fact]
    public void Brackets_WithTheNumberTool_StillStepItsSize()
    {
        Assert.Equal(OverlayCommand.PenSizeUp, Router.RouteText("]", Numbering()));
    }

    // ---------------------------------------------------------------- annotation copy ---

    [Fact]
    public void CtrlShiftC_WithAnAnnotationSelected_CopiesIt()
    {
        var ctx = Editing(Key.C, Ctrl | Shift) with { HasSelection = true };

        Assert.Equal(OverlayCommand.CopyAnnotation, Router.Route(ctx));
    }

    [Fact]
    public void CtrlShiftC_WithNothingSelected_DoesNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.C, Ctrl | Shift)));
    }

    [Fact]
    public void CtrlShiftV_PastesTheCopiedAnnotation()
    {
        Assert.Equal(OverlayCommand.PasteAnnotation, Router.Route(Editing(Key.V, Ctrl | Shift)));
    }

    [Fact]
    public void AnnotationCopyAndPaste_WhileSelectingTypingOrMidDrag_DoNothing()
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(Key.V, Ctrl | Shift)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.V, Ctrl | Shift)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.C, Ctrl | Shift) with { TextBoxDeclined = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.C, Ctrl | Shift) with { HasSelection = true, Dragging = true }));
    }

    [Fact]
    public void CtrlC_AndCtrlV_AreNotTheAnnotationCopy()
    {
        var ctx = Editing(Key.C, Ctrl) with { HasSelection = true };

        Assert.Equal(OverlayCommand.Copy, Router.Route(ctx));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.V, Ctrl)));
    }

    [Fact]
    public void AGestureBoundToCtrlShiftC_WinsOverTheAnnotationCopy()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Copy = new KeyChord(Key.C, Ctrl | Shift) };
        var ctx = Editing(Key.C, Ctrl | Shift) with { HasSelection = true };

        Assert.Equal(OverlayCommand.Copy, new OverlayKeyRouter(bindings).Route(ctx));
    }

    // ---------------------------------------------------------------- line strip ---

    private static OverlayKeyContext Stripping(Key key, ModifierKeys modifiers = None) =>
        Editing(key, modifiers) with { Tool = ToolKind.Line, LineStripActive = true };

    [Fact]
    public void Esc_WhileDrawingALineStrip_FinishesIt_BeforeDeselecting()
    {
        var ctx = Stripping(Key.Escape) with { HasSelection = true };

        Assert.Equal(OverlayCommand.FinishLineStrip, Router.Route(ctx));
    }

    [Fact]
    public void Enter_WhileDrawingALineStrip_FinishesIt_NotTheEnterAction()
    {
        Assert.Equal(OverlayCommand.FinishLineStrip, Router.Route(Stripping(Key.Enter)));
    }

    [Fact]
    public void ChordedEnter_WhileDrawingALineStrip_DoesNotFinishIt()
    {
        Assert.NotEqual(OverlayCommand.FinishLineStrip, Router.Route(Stripping(Key.Enter, Ctrl)));
    }

    [Fact]
    public void OtherKeys_WhileDrawingALineStrip_KeepTheirMeaning()
    {
        Assert.Equal(OverlayCommand.Undo, Router.Route(Stripping(Key.Z, Ctrl)));
        Assert.Equal(OverlayCommand.ToggleToolVariant, Router.Route(Stripping(Key.Tab)));
    }

    [Fact]
    public void EscAndEnter_WithoutALineStrip_AreUnchanged()
    {
        var line = Editing(Key.Escape) with { Tool = ToolKind.Line };

        Assert.Equal(OverlayCommand.ConfirmClose, Router.Route(line));
        Assert.Equal(OverlayCommand.RunEnterAction, Router.Route(line with { Key = Key.Enter }));
    }

    // ---------------------------------------------------------------- snip context ---

    public static TheoryData<OverlayPhase> BothPhases() => new() { OverlayPhase.Selecting, OverlayPhase.Editing };

    private static OverlayKeyContext InPhase(OverlayPhase phase, Key key, ModifierKeys modifiers = None) =>
        Selecting(key, modifiers) with { Phase = phase };

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void R_StepsBackToTheLastSnipsArea_InEitherPhase(OverlayPhase phase)
    {
        Assert.Equal(OverlayCommand.PreviousRegion, Router.Route(InPhase(phase, Key.R)));
    }

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void ShiftR_StepsTheOtherWay(OverlayPhase phase)
    {
        Assert.Equal(OverlayCommand.NextRegion, Router.Route(InPhase(phase, Key.R, Shift)));
    }

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void F5_RefreshesTheScreenshot_InEitherPhase(OverlayPhase phase)
    {
        Assert.Equal(OverlayCommand.RefreshScreenshot, Router.Route(InPhase(phase, Key.F5)));
    }

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void F5_HeldDown_KeepsRefreshing(OverlayPhase phase)
    {
        var ctx = InPhase(phase, Key.F5) with { IsRepeat = true };

        Assert.Equal(OverlayCommand.RefreshScreenshot, Router.Route(ctx));
    }

    [Theory]
    [InlineData(Key.R, Ctrl)]
    [InlineData(Key.R, Alt)]
    [InlineData(Key.F5, Shift)]
    [InlineData(Key.F5, Ctrl)]
    public void ChordedSnipKeys_AreNotBound(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(Selecting(key, modifiers)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(key, modifiers)));
    }

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void SnipKeys_MidDrag_DoNothing(OverlayPhase phase)
    {
        Assert.Equal(OverlayCommand.None, Router.Route(InPhase(phase, Key.R) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.Route(InPhase(phase, Key.F5) with { Dragging = true }));
    }

    [Fact]
    public void SnipKeys_WhileTyping_AreTyping()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.R)));
        Assert.Equal(OverlayCommand.PassThrough, Router.Route(Typing(Key.F5)));
    }

    [Fact]
    public void R_WithTheNumberTool_IsStillTheLastArea()
    {
        var ctx = Editing(Key.R) with { Tool = ToolKind.Number };

        Assert.Equal(OverlayCommand.PreviousRegion, Router.Route(ctx));
    }

    [Fact]
    public void AGestureBoundToF5_WinsWhileEditing()
    {
        var bindings = OverlayKeyBindings.FromSettings(new AppSettings()) with { Save = new KeyChord(Key.F5, None) };

        Assert.Equal(OverlayCommand.Save, new OverlayKeyRouter(bindings).Route(Editing(Key.F5)));
    }

    [Theory]
    [InlineData("`", OverlayCommand.ToggleCursor)]
    [InlineData("!", OverlayCommand.ToggleCursor)]
    [InlineData(",", OverlayCommand.HistoryPrevious)]
    [InlineData(".", OverlayCommand.HistoryNext)]
    public void SnipContextCharacters_WhileSelecting(string text, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.RouteText(text, Selecting(Key.None)));
    }

    [Theory]
    [InlineData("`", OverlayCommand.ToggleCursor)]
    [InlineData("!", OverlayCommand.ToggleCursor)]
    [InlineData(",", OverlayCommand.HistoryPrevious)]
    [InlineData(".", OverlayCommand.HistoryNext)]
    public void SnipContextCharacters_WhileEditing(string text, OverlayCommand expected)
    {
        Assert.Equal(expected, Router.RouteText(text, Editing(Key.None)));
    }

    [Theory]
    [InlineData(Shift)]       // ! over 1 on a US keyboard
    [InlineData(None)]        // ! has a key of its own on a French one
    [InlineData(Ctrl | Alt)]  // AltGr
    public void ExclamationMark_MatchesByCharacter_WhateverModifiersTypedIt(ModifierKeys modifiers)
    {
        Assert.Equal(OverlayCommand.ToggleCursor, Router.RouteText("!", Selecting(Key.None, modifiers)));
    }

    [Fact]
    public void SnipContextCharacters_WithTheNumberTool_KeepTheirMeaning()
    {
        Assert.Equal(OverlayCommand.HistoryNext, Router.RouteText(".", Numbering()));
        Assert.Equal(OverlayCommand.ToggleCursor, Router.RouteText("!", Numbering()));
    }

    [Fact]
    public void SnipContextCharacters_WhileTypingOrInTheFontList_AreLeftToTheControl()
    {
        Assert.Equal(OverlayCommand.PassThrough, Router.RouteText(",", Typing(Key.None)));
        Assert.Equal(OverlayCommand.PassThrough, Router.RouteText("`", Editing(Key.None) with { DropDownOpen = true }));
    }

    [Theory]
    [MemberData(nameof(BothPhases))]
    public void SnipContextCharacters_MidDrag_DoNothing(OverlayPhase phase)
    {
        Assert.Equal(OverlayCommand.None, Router.RouteText(",", InPhase(phase, Key.None) with { Dragging = true }));
        Assert.Equal(OverlayCommand.None, Router.RouteText("`", InPhase(phase, Key.None) with { Dragging = true }));
    }

    [Fact]
    public void Digit1_IsStillThePenSize_NotTheCursorToggle()
    {
        Assert.Equal(OverlayCommand.PenSizeDown, Router.Route(Editing(Key.D1)));
        Assert.Equal(OverlayCommand.None, Router.Route(Editing(Key.D1, Shift)));   // types "!" instead
    }
}
