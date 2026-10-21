namespace ScreenPaste.Editor;

/// <summary>
/// What the annotation tool keys do: Ctrl+0..9 choose a tool by Snipaste's meaning
/// (ScreenPaste's toolbar keeps its own order), Ctrl+Tab cycles through the toolbar, and Tab
/// flips the current tool's variant. Which keys they are is the user's
/// (<see cref="ScreenPaste.Settings.ShortcutActions"/>); which context a key works in is
/// <see cref="ScreenPaste.Capture.OverlayKeyRouter"/>'s. Pure, so it is unit tested.
/// </summary>
public static class ToolShortcuts
{
    /// <summary>Ctrl+digit, indexed by the digit. None would mark a Snipaste tool ScreenPaste
    /// does not have.</summary>
    public static readonly IReadOnlyList<ToolKind> SnipasteLayout = new[]
    {
        ToolKind.Sticker,      // Ctrl+0: ScreenPaste's own, Snipaste has no tool here
        ToolKind.Shape,        // Ctrl+1: rectangle / ellipse
        ToolKind.Line,         // Ctrl+2: line / arrow
        ToolKind.MarkerPen,    // Ctrl+3: Snipaste's pencil
        ToolKind.Highlighter,  // Ctrl+4: Snipaste's marker
        ToolKind.Blur,         // Ctrl+5: mosaic / blur
        ToolKind.Text,         // Ctrl+6
        ToolKind.Number,       // Ctrl+7: number marker
        ToolKind.Magnify,      // Ctrl+8
        ToolKind.Eraser,       // Ctrl+9
    };

    /// <summary>What Ctrl+Tab steps through: the toolbar order, without the sticker tool,
    /// which opens a file dialog when no image has been placed yet.</summary>
    public static readonly IReadOnlyList<ToolKind> CycleOrder = new[]
    {
        ToolKind.MarkerPen, ToolKind.Highlighter, ToolKind.Text, ToolKind.Shape,
        ToolKind.Line, ToolKind.Blur, ToolKind.Magnify, ToolKind.Number, ToolKind.Eraser,
    };

    /// <summary>
    /// The tool <paramref name="step"/> places after <paramref name="current"/> in
    /// <paramref name="order"/>, wrapping around and skipping None. From no tool (or one not in
    /// the order) a forward step starts at the first tool and a backward one at the last.
    /// </summary>
    public static ToolKind Cycle(IReadOnlyList<ToolKind> order, ToolKind current, int step)
    {
        var tools = order.Where(t => t != ToolKind.None).ToList();
        if (tools.Count == 0 || step == 0) return current;

        int at = tools.IndexOf(current);
        if (at < 0) return step > 0 ? tools[0] : tools[^1];
        int n = tools.Count;
        return tools[((at + step) % n + n) % n];
    }

    /// <summary>Tab with the shape tool: rectangle, rounded rectangle, ellipse, and round.</summary>
    public static ShapeKind NextShapeKind(ShapeKind kind) => kind switch
    {
        ShapeKind.Rectangle => ShapeKind.RoundedRectangle,
        ShapeKind.RoundedRectangle => ShapeKind.Ellipse,
        _ => ShapeKind.Rectangle,
    };

    /// <summary>Tab with the blur tool: Gaussian and mosaic.</summary>
    public static BlurKind NextBlurKind(BlurKind kind) =>
        kind == BlurKind.Gaussian ? BlurKind.Mosaic : BlurKind.Gaussian;

    /// <summary>The tools whose variant Tab flips: the shape (and the magnifier's frame)
    /// kind, line or arrow, Gaussian or mosaic.</summary>
    public static bool HasVariant(ToolKind tool) =>
        tool is ToolKind.Shape or ToolKind.Line or ToolKind.Blur or ToolKind.Magnify;
}
