using System.Windows.Input;
using ScreenPaste.Editor;
using ScreenPaste.Input;

namespace ScreenPaste.Settings;

/// <summary>Where an in-app shortcut works.</summary>
public enum ShortcutArea { Capture, Pin }

/// <summary>When an action can run, so the settings window warns only about keys two actions
/// really share (Space takes the detected area while framing, toggles the toolbar while
/// annotating, and moves the area while a button is held: no clash).</summary>
[Flags]
public enum ShortcutContexts
{
    None = 0,
    /// <summary>Framing the area, no button held.</summary>
    Selecting = 1,
    /// <summary>Framing the area with the left button held.</summary>
    SelectingDrag = 2,
    /// <summary>Annotating, no button held.</summary>
    Editing = 4,
    /// <summary>Annotating with the left button held: drawing, moving or resizing.</summary>
    EditingDrag = 8,
    /// <summary>A focused pin.</summary>
    Pin = 16,

    /// <summary>Either phase, no button held.</summary>
    Idle = Selecting | Editing,
    /// <summary>Anywhere in the capture.</summary>
    Capture = Selecting | SelectingDrag | Editing | EditingDrag,
}

/// <summary>
/// One action of the capture or a pin the user can bind keys to: its settings id, where and
/// when it works, its label (a Loc key, with <see cref="LabelArg"/> for an arrow), and its
/// default keys (<see cref="ShortcutKey"/> text). A directional action names the key its
/// executor reads the direction from (<see cref="Canonical"/>); a tool action, its tool. The
/// few that had a single gesture setting before (CopyHotkey ...) read it when settings.json
/// has no list for them, and the capture's keep it in step (the recording editor reads it).
/// </summary>
public sealed record ShortcutAction(
    string Id, ShortcutArea Area, string Group, string LabelKey, IReadOnlyList<string> Defaults, ShortcutContexts Contexts)
{
    public string? LabelArg { get; init; }
    public KeyChord? Canonical { get; init; }
    public ToolKind? Tool { get; init; }
    public Func<AppSettings, string>? LegacyGet { get; init; }
    public Action<AppSettings, string>? LegacySet { get; init; }

    public IReadOnlyList<ShortcutKey> DefaultKeys { get; } =
        Defaults.Select(ShortcutKey.Parse).OfType<ShortcutKey>().ToArray();
}

/// <summary>The capture overlay's action ids (settings.json keys of <see cref="AppSettings.KeyBindings"/>).</summary>
public static class CaptureShortcuts
{
    public const string Escape = "capture.escape";
    public const string DetectMode = "capture.detectMode";
    public const string AcceptDetected = "capture.acceptDetected";
    public const string HoldToMove = "capture.holdToMove";
    public const string ScreenArea = "capture.screenArea";
    public const string GuideLines = "capture.guideLines";
    public const string NudgeUp = "capture.nudgeUp";
    public const string NudgeDown = "capture.nudgeDown";
    public const string NudgeLeft = "capture.nudgeLeft";
    public const string NudgeRight = "capture.nudgeRight";
    public const string MoveUp = "capture.moveUp";
    public const string MoveDown = "capture.moveDown";
    public const string MoveLeft = "capture.moveLeft";
    public const string MoveRight = "capture.moveRight";
    public const string GrowUp = "capture.growUp";
    public const string GrowDown = "capture.growDown";
    public const string GrowLeft = "capture.growLeft";
    public const string GrowRight = "capture.growRight";
    public const string ShrinkUp = "capture.shrinkUp";
    public const string ShrinkDown = "capture.shrinkDown";
    public const string ShrinkLeft = "capture.shrinkLeft";
    public const string ShrinkRight = "capture.shrinkRight";
    public const string Copy = "capture.copy";
    public const string Save = "capture.save";
    public const string QuickSave = "capture.quickSave";
    public const string Pin = "capture.pin";
    public const string Print = "capture.print";
    public const string CopyAsFile = "capture.copyAsFile";
    public const string CopySize = "capture.copySize";
    public const string EnterAction = "capture.enterAction";
    public const string ToggleToolbar = "capture.toggleToolbar";
    public const string Undo = "capture.undo";
    public const string Redo = "capture.redo";
    public const string ClearAll = "capture.clearAll";
    public const string Delete = "capture.delete";
    public const string CopyAnnotation = "capture.copyAnnotation";
    public const string PasteAnnotation = "capture.pasteAnnotation";
    public const string PenSizeDown = "capture.penSizeDown";
    public const string PenSizeUp = "capture.penSizeUp";
    public const string NumberDown = "capture.numberDown";
    public const string NumberUp = "capture.numberUp";
    public const string PickColor = "capture.pickColor";
    public const string ToolVariant = "capture.toolVariant";
    public const string NextTool = "capture.nextTool";
    public const string PreviousTool = "capture.previousTool";
    public const string ToolShape = "capture.tool.shape";
    public const string ToolLine = "capture.tool.line";
    public const string ToolMarker = "capture.tool.marker";
    public const string ToolHighlighter = "capture.tool.highlighter";
    public const string ToolBlur = "capture.tool.blur";
    public const string ToolText = "capture.tool.text";
    public const string ToolNumber = "capture.tool.number";
    public const string ToolMagnify = "capture.tool.magnify";
    public const string ToolEraser = "capture.tool.eraser";
    public const string ToolSticker = "capture.tool.sticker";
    public const string CopyColor = "capture.copyColor";
    public const string ColorFormat = "capture.colorFormat";
    public const string ToggleCursor = "capture.toggleCursor";
    public const string PreviousRegion = "capture.previousRegion";
    public const string NextRegion = "capture.nextRegion";
    public const string HistoryPrevious = "capture.historyPrevious";
    public const string HistoryNext = "capture.historyNext";
    public const string Refresh = "capture.refresh";

    public static readonly IReadOnlyList<string> Nudges = new[] { NudgeUp, NudgeDown, NudgeLeft, NudgeRight };
    public static readonly IReadOnlyList<string> Moves = new[] { MoveUp, MoveDown, MoveLeft, MoveRight };
    public static readonly IReadOnlyList<string> Grows = new[] { GrowUp, GrowDown, GrowLeft, GrowRight };
    public static readonly IReadOnlyList<string> Shrinks = new[] { ShrinkUp, ShrinkDown, ShrinkLeft, ShrinkRight };

    /// <summary>The tool keys, in Snipaste's Ctrl+1..9, Ctrl+0 order.</summary>
    public static readonly IReadOnlyList<string> Tools = new[]
    {
        ToolShape, ToolLine, ToolMarker, ToolHighlighter, ToolBlur, ToolText, ToolNumber, ToolMagnify, ToolEraser, ToolSticker,
    };

    /// <summary>The action that picks <paramref name="tool"/>, or null.</summary>
    public static string? ForTool(ToolKind tool) =>
        ShortcutActions.All.FirstOrDefault(a => a.Area == ShortcutArea.Capture && a.Tool == tool)?.Id;
}

/// <summary>A pin's action ids (settings.json keys of <see cref="AppSettings.KeyBindings"/>).</summary>
public static class PinShortcuts
{
    public const string Close = "pin.close";
    public const string Destroy = "pin.destroy";
    public const string Menu = "pin.menu";
    public const string Settings = "pin.settings";
    public const string SelectAll = "pin.selectAll";
    public const string MoveUp = "pin.moveUp";
    public const string MoveDown = "pin.moveDown";
    public const string MoveLeft = "pin.moveLeft";
    public const string MoveRight = "pin.moveRight";
    public const string NudgeUp = "pin.nudgeUp";
    public const string NudgeDown = "pin.nudgeDown";
    public const string NudgeLeft = "pin.nudgeLeft";
    public const string NudgeRight = "pin.nudgeRight";
    public const string RotateClockwise = "pin.rotateClockwise";
    public const string RotateCounterClockwise = "pin.rotateCounterClockwise";
    public const string FlipHorizontal = "pin.flipHorizontal";
    public const string FlipVertical = "pin.flipVertical";
    public const string Grayscale = "pin.grayscale";
    public const string Invert = "pin.invert";
    public const string ZoomIn = "pin.zoomIn";
    public const string ZoomOut = "pin.zoomOut";
    public const string OpacityUp = "pin.opacityUp";
    public const string OpacityDown = "pin.opacityDown";
    public const string Copy = "pin.copy";
    public const string CopyAsFile = "pin.copyAsFile";
    public const string Save = "pin.save";
    public const string QuickSave = "pin.quickSave";
    public const string Print = "pin.print";
    public const string Paste = "pin.paste";
    public const string CopyText = "pin.copyText";
    public const string Loupe = "pin.loupe";
    public const string CopyColor = "pin.copyColor";
    public const string ColorFormat = "pin.colorFormat";

    public static readonly IReadOnlyList<string> Moves = new[] { MoveUp, MoveDown, MoveLeft, MoveRight };
    public static readonly IReadOnlyList<string> Nudges = new[] { NudgeUp, NudgeDown, NudgeLeft, NudgeRight };
}

/// <summary>
/// The table of every rebindable in-app action, in the order the settings window lists them.
/// The defaults are Snipaste's keys; <see cref="ShortcutMap"/> applies the user's changes.
/// </summary>
public static class ShortcutActions
{
    public const string GroupSelect = "keys.group.select";
    public const string GroupRegion = "keys.group.region";
    public const string GroupOutput = "keys.group.output";
    public const string GroupAnnotate = "keys.group.annotate";
    public const string GroupTools = "keys.group.tools";
    public const string GroupLoupe = "keys.group.loupe";
    public const string GroupSnip = "keys.group.snip";
    public const string GroupPin = "keys.group.pin";
    public const string GroupPinView = "keys.group.pinView";
    public const string GroupPinOutput = "keys.group.pinOutput";
    public const string GroupPinLoupe = "keys.group.pinLoupe";

    private static readonly (string Arrow, Key ArrowKey, Key Wasd)[] Directions =
    {
        ("↑", Key.Up, Key.W), ("↓", Key.Down, Key.S), ("←", Key.Left, Key.A), ("→", Key.Right, Key.D),
    };

    private const ShortcutContexts Gesture = ShortcutContexts.Editing | ShortcutContexts.EditingDrag;

    public static IReadOnlyList<ShortcutAction> All { get; } = Capture().Concat(Pins()).ToArray();

    private static readonly Dictionary<string, ShortcutAction> Index = All.ToDictionary(a => a.Id);

    public static ShortcutAction? ById(string id) => Index.GetValueOrDefault(id);

    private static IEnumerable<ShortcutAction> Capture()
    {
        const ShortcutArea c = ShortcutArea.Capture;

        yield return new(CaptureShortcuts.DetectMode, c, GroupSelect, "keys.detectMode", new[] { "Tab" }, ShortcutContexts.Selecting);
        yield return new(CaptureShortcuts.AcceptDetected, c, GroupSelect, "keys.acceptDetected", new[] { "Space" }, ShortcutContexts.Selecting);
        yield return new(CaptureShortcuts.HoldToMove, c, GroupSelect, "keys.holdToMove", new[] { "Space" },
            ShortcutContexts.SelectingDrag | ShortcutContexts.EditingDrag);
        yield return new(CaptureShortcuts.ScreenArea, c, GroupSelect, "keys.screenArea", new[] { "Ctrl+A" }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.GuideLines, c, GroupSelect, "keys.guideLines", new[] { "Alt" }, ShortcutContexts.Selecting);
        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(CaptureShortcuts.Nudges[i], c, GroupSelect, "keys.nudgeCursor", new[] { Name(d.Wasd) }, ShortcutContexts.Capture)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.Wasd, ModifierKeys.None) };
        }

        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(CaptureShortcuts.Moves[i], c, GroupRegion, "keys.moveArea", new[] { Name(d.ArrowKey) },
                ShortcutContexts.Selecting | ShortcutContexts.SelectingDrag | ShortcutContexts.Editing)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.ArrowKey, ModifierKeys.None) };
        }
        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(CaptureShortcuts.Grows[i], c, GroupRegion, "keys.growArea", new[] { "Ctrl+" + Name(d.ArrowKey) }, ShortcutContexts.Editing)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.ArrowKey, ModifierKeys.Control) };
        }
        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(CaptureShortcuts.Shrinks[i], c, GroupRegion, "keys.shrinkArea", new[] { "Shift+" + Name(d.ArrowKey) }, ShortcutContexts.Editing)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.ArrowKey, ModifierKeys.Shift) };
        }

        yield return new(CaptureShortcuts.Copy, c, GroupOutput, "action.copy", new[] { "Ctrl+C", "Ctrl+Insert" }, Gesture)
            { LegacyGet = s => s.CopyHotkey, LegacySet = (s, v) => s.CopyHotkey = v };
        yield return new(CaptureShortcuts.Save, c, GroupOutput, "action.save", new[] { "Ctrl+S" }, Gesture)
            { LegacyGet = s => s.SaveHotkey, LegacySet = (s, v) => s.SaveHotkey = v };
        yield return new(CaptureShortcuts.QuickSave, c, GroupOutput, "set.quickSave", new[] { "Ctrl+Shift+S" }, Gesture)
            { LegacyGet = s => s.QuickSaveHotkey, LegacySet = (s, v) => s.QuickSaveHotkey = v };
        yield return new(CaptureShortcuts.Pin, c, GroupOutput, "action.pin", new[] { "Ctrl+T" }, Gesture)
            { LegacyGet = s => s.PinHotkey, LegacySet = (s, v) => s.PinHotkey = v };
        yield return new(CaptureShortcuts.Print, c, GroupOutput, "set.print", new[] { "Ctrl+P" }, Gesture)
            { LegacyGet = s => s.PrintHotkey, LegacySet = (s, v) => s.PrintHotkey = v };
        yield return new(CaptureShortcuts.CopyAsFile, c, GroupOutput, "pin.copyFile", new[] { "Ctrl+Alt+C" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.CopySize, c, GroupOutput, "keys.copySize", new[] { "V" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.EnterAction, c, GroupOutput, "keys.enterAction", new[] { "Enter" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.ToggleToolbar, c, GroupOutput, "keys.toggleToolbar", new[] { "Space" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.Escape, c, GroupOutput, "keys.escape", new[] { "Esc" }, ShortcutContexts.Capture);

        yield return new(CaptureShortcuts.Undo, c, GroupAnnotate, "action.undo", new[] { "Ctrl+Z" }, Gesture)
            { LegacyGet = s => s.UndoHotkey, LegacySet = (s, v) => s.UndoHotkey = v };
        yield return new(CaptureShortcuts.Redo, c, GroupAnnotate, "action.redo", new[] { "Ctrl+Y" }, Gesture)
            { LegacyGet = s => s.RedoHotkey, LegacySet = (s, v) => s.RedoHotkey = v };
        yield return new(CaptureShortcuts.ClearAll, c, GroupAnnotate, "action.clearAll", new[] { "Ctrl+Shift+Z" }, ShortcutContexts.Editing)
            { LegacyGet = s => s.ClearAllHotkey, LegacySet = (s, v) => s.ClearAllHotkey = v };
        yield return new(CaptureShortcuts.Delete, c, GroupAnnotate, "keys.deleteSelected", new[] { "Delete" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.CopyAnnotation, c, GroupAnnotate, "keys.copyAnnotation", new[] { "Ctrl+Shift+C" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.PasteAnnotation, c, GroupAnnotate, "keys.pasteAnnotation", new[] { "Ctrl+Shift+V" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.PenSizeDown, c, GroupAnnotate, "keys.penThinner", new[] { "1", "[" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.PenSizeUp, c, GroupAnnotate, "keys.penThicker", new[] { "2", "]" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.NumberDown, c, GroupAnnotate, "keys.numberDown", new[] { "-" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.NumberUp, c, GroupAnnotate, "keys.numberUp", new[] { "+", "=" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.PickColor, c, GroupAnnotate, "keys.pickColor", new[] { "Alt+C" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.ToolVariant, c, GroupAnnotate, "keys.toolVariant", new[] { "Tab" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.NextTool, c, GroupAnnotate, "keys.nextTool", new[] { "Ctrl+Tab" }, ShortcutContexts.Editing);
        yield return new(CaptureShortcuts.PreviousTool, c, GroupAnnotate, "keys.previousTool", new[] { "Ctrl+Shift+Tab" }, ShortcutContexts.Editing);

        foreach (var action in ToolActions()) yield return action;

        yield return new(CaptureShortcuts.CopyColor, c, GroupLoupe, "keys.copyColor", new[] { "C" }, ShortcutContexts.Capture);
        yield return new(CaptureShortcuts.ColorFormat, c, GroupLoupe, "keys.colorFormat", new[] { "Shift" }, ShortcutContexts.Idle);

        yield return new(CaptureShortcuts.ToggleCursor, c, GroupSnip, "keys.toggleCursor", new[] { "`", "!" }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.PreviousRegion, c, GroupSnip, "keys.previousRegion", new[] { "R" }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.NextRegion, c, GroupSnip, "keys.nextRegion", new[] { "Shift+R" }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.HistoryPrevious, c, GroupSnip, "keys.historyPrevious", new[] { "," }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.HistoryNext, c, GroupSnip, "keys.historyNext", new[] { "." }, ShortcutContexts.Idle);
        yield return new(CaptureShortcuts.Refresh, c, GroupSnip, "keys.refresh", new[] { "F5" }, ShortcutContexts.Idle);
    }

    /// <summary>Snipaste's Ctrl+1..9 and Ctrl+0 (<see cref="ToolShortcuts.SnipasteLayout"/>),
    /// plus its letters T / B / M / E.</summary>
    private static IEnumerable<ShortcutAction> ToolActions()
    {
        var labels = new Dictionary<ToolKind, string>
        {
            [ToolKind.Shape] = "tool.shape", [ToolKind.Line] = "tool.line", [ToolKind.MarkerPen] = "tool.marker",
            [ToolKind.Highlighter] = "tool.highlighter", [ToolKind.Blur] = "tool.blur", [ToolKind.Text] = "tool.text",
            [ToolKind.Number] = "tool.number", [ToolKind.Magnify] = "tool.magnify", [ToolKind.Eraser] = "tool.eraser",
            [ToolKind.Sticker] = "tool.sticker",
        };
        var letters = new Dictionary<ToolKind, string>
        {
            [ToolKind.MarkerPen] = "B", [ToolKind.Blur] = "M", [ToolKind.Text] = "T", [ToolKind.Eraser] = "E",
        };
        for (int n = 1; n <= 10; n++)
        {
            int digit = n % 10;   // Ctrl+1 .. Ctrl+9, then Ctrl+0
            var tool = ToolShortcuts.SnipasteLayout[digit];
            var keys = letters.TryGetValue(tool, out var letter)
                ? new[] { letter, "Ctrl+" + digit }
                : new[] { "Ctrl+" + digit };
            yield return new(CaptureShortcuts.Tools[n - 1], ShortcutArea.Capture, GroupTools, labels[tool], keys, ShortcutContexts.Editing)
                { Tool = tool };
        }
    }

    private static IEnumerable<ShortcutAction> Pins()
    {
        const ShortcutArea p = ShortcutArea.Pin;
        const ShortcutContexts pin = ShortcutContexts.Pin;

        yield return new(PinShortcuts.Close, p, GroupPin, "pin.close", new[] { "Esc", "Ctrl+W" }, pin);
        yield return new(PinShortcuts.Destroy, p, GroupPin, "pin.destroy", new[] { "Shift+Esc" }, pin);
        yield return new(PinShortcuts.Menu, p, GroupPin, "keys.pinMenu", new[] { "Apps" }, pin);
        yield return new(PinShortcuts.Settings, p, GroupPin, "keys.openSettings", new[] { "Ctrl+Shift+P" }, pin);
        yield return new(PinShortcuts.SelectAll, p, GroupPin, "keys.selectAllPins", new[] { "Ctrl+A" }, pin);
        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(PinShortcuts.Moves[i], p, GroupPin, "keys.movePin", new[] { Name(d.ArrowKey) }, pin)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.ArrowKey, ModifierKeys.None) };
        }
        for (int i = 0; i < Directions.Length; i++)
        {
            var d = Directions[i];
            yield return new(PinShortcuts.Nudges[i], p, GroupPin, "keys.nudgeCursor", new[] { Name(d.Wasd) }, pin)
                { LabelArg = d.Arrow, Canonical = new KeyChord(d.Wasd, ModifierKeys.None) };
        }

        yield return new(PinShortcuts.RotateClockwise, p, GroupPinView, "pin.rotateCw", new[] { "1" }, pin);
        yield return new(PinShortcuts.RotateCounterClockwise, p, GroupPinView, "pin.rotateCcw", new[] { "2" }, pin);
        yield return new(PinShortcuts.FlipHorizontal, p, GroupPinView, "pin.flipH", new[] { "3" }, pin);
        yield return new(PinShortcuts.FlipVertical, p, GroupPinView, "pin.flipV", new[] { "4" }, pin);
        yield return new(PinShortcuts.Grayscale, p, GroupPinView, "pin.grayscale", new[] { "5" }, pin);
        yield return new(PinShortcuts.Invert, p, GroupPinView, "pin.invert", new[] { "6" }, pin);
        yield return new(PinShortcuts.ZoomIn, p, GroupPinView, "keys.zoomIn", new[] { "+", "=" }, pin);
        yield return new(PinShortcuts.ZoomOut, p, GroupPinView, "keys.zoomOut", new[] { "-" }, pin);
        yield return new(PinShortcuts.OpacityUp, p, GroupPinView, "pin.opacityUp", new[] { "Ctrl++", "Ctrl+=" }, pin);
        yield return new(PinShortcuts.OpacityDown, p, GroupPinView, "pin.opacityDown", new[] { "Ctrl+-" }, pin);

        yield return new(PinShortcuts.Copy, p, GroupPinOutput, "pin.copy", new[] { "Ctrl+C", "Ctrl+Insert" }, pin)
            { LegacyGet = s => s.CopyHotkey };
        yield return new(PinShortcuts.CopyAsFile, p, GroupPinOutput, "pin.copyFile", new[] { "Ctrl+Alt+C" }, pin);
        yield return new(PinShortcuts.Save, p, GroupPinOutput, "pin.save", new[] { "Ctrl+S" }, pin) { LegacyGet = s => s.SaveHotkey };
        yield return new(PinShortcuts.QuickSave, p, GroupPinOutput, "pin.quickSave", new[] { "Ctrl+Shift+S" }, pin)
            { LegacyGet = s => s.QuickSaveHotkey };
        yield return new(PinShortcuts.Print, p, GroupPinOutput, "pin.print", new[] { "Ctrl+P" }, pin) { LegacyGet = s => s.PrintHotkey };
        yield return new(PinShortcuts.Paste, p, GroupPinOutput, "pin.paste", new[] { "Ctrl+V" }, pin);
        yield return new(PinShortcuts.CopyText, p, GroupPinOutput, "pin.copyText", new[] { "Ctrl+Shift+C" }, pin);

        yield return new(PinShortcuts.Loupe, p, GroupPinLoupe, "keys.pinLoupe", new[] { "Alt" }, pin);
        yield return new(PinShortcuts.CopyColor, p, GroupPinLoupe, "keys.copyColor", new[] { "C" }, pin);
        yield return new(PinShortcuts.ColorFormat, p, GroupPinLoupe, "keys.colorFormat", new[] { "Shift" }, pin);
    }

    private static string Name(Key key) => new KeyChord(key, ModifierKeys.None).ToString();
}
