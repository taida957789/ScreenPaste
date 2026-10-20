using System.Windows.Input;
using ScreenPaste.Input;

namespace ScreenPaste.Editor;

/// <summary>
/// Snipaste's keys for picking and adjusting the annotation tools: T / B / M choose a tool,
/// Ctrl+0..9 choose one by Snipaste's meaning (ScreenPaste's toolbar keeps its own order),
/// Ctrl+Tab cycles through the toolbar, Tab flips the current tool's variant, and 1 / 2 or
/// [ / ] step its width. Pure, so the maps are unit tested; which context a key works in
/// is <see cref="ScreenPaste.Capture.OverlayKeyRouter"/>'s.
/// </summary>
public static class ToolShortcuts
{
    /// <summary>Ctrl+digit, indexed by the digit. None marks a Snipaste tool ScreenPaste
    /// does not have (7 is Snipaste's number tool, 9 its eraser).</summary>
    public static readonly IReadOnlyList<ToolKind> SnipasteLayout = new[]
    {
        ToolKind.Sticker,      // Ctrl+0: ScreenPaste's own, Snipaste has no tool here
        ToolKind.Shape,        // Ctrl+1: rectangle / ellipse
        ToolKind.Line,         // Ctrl+2: line / arrow
        ToolKind.MarkerPen,    // Ctrl+3: Snipaste's pencil
        ToolKind.Highlighter,  // Ctrl+4: Snipaste's marker
        ToolKind.Blur,         // Ctrl+5: mosaic / blur
        ToolKind.Text,         // Ctrl+6
        ToolKind.None,         // Ctrl+7: number
        ToolKind.Magnify,      // Ctrl+8
        ToolKind.None,         // Ctrl+9: eraser
    };

    /// <summary>What Ctrl+Tab steps through: the toolbar order, without the sticker tool,
    /// which opens a file dialog when no image has been placed yet.</summary>
    public static readonly IReadOnlyList<ToolKind> CycleOrder = new[]
    {
        ToolKind.MarkerPen, ToolKind.Highlighter, ToolKind.Text, ToolKind.Shape,
        ToolKind.Line, ToolKind.Blur, ToolKind.Magnify,
    };

    private static readonly (Key Key, ToolKind Tool)[] Letters =
    {
        (Key.T, ToolKind.Text),
        (Key.B, ToolKind.MarkerPen),
        (Key.M, ToolKind.Blur),
    };

    /// <summary>The tool a bare letter picks, or null.</summary>
    public static ToolKind? ToolForLetter(Key key)
    {
        foreach (var (letter, tool) in Letters)
            if (letter == key) return tool;
        return null;
    }

    /// <summary>The tool at <paramref name="index"/> in <paramref name="layout"/>, or null when
    /// the index is out of range or holds no tool.</summary>
    public static ToolKind? ToolForIndex(int index, IReadOnlyList<ToolKind> layout) =>
        index >= 0 && index < layout.Count && layout[index] != ToolKind.None ? layout[index] : null;

    /// <summary>The digit on a top-row or number-pad key, or null.</summary>
    public static int? DigitOf(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
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

    /// <summary>-1 for the keys that make the pen thinner (1), +1 for thicker (2), else 0.
    /// [ and ] depend on the keyboard layout, so they go by character instead.</summary>
    public static int PenSizeDirection(Key key) => key switch
    {
        Key.D1 or Key.NumPad1 => -1,
        Key.D2 or Key.NumPad2 => 1,
        _ => 0,
    };

    /// <summary>-1 for a typed "[", +1 for "]", else 0.</summary>
    public static int PenSizeDirection(string text) => text switch
    {
        "[" => -1,
        "]" => 1,
        _ => 0,
    };

    /// <summary>"Ctrl+3, B": every key that picks <paramref name="tool"/>, for its tooltip;
    /// empty when none does.</summary>
    public static string KeysLabel(ToolKind tool)
    {
        var keys = new List<string>();
        for (int i = 0; i < SnipasteLayout.Count; i++)
            if (tool != ToolKind.None && SnipasteLayout[i] == tool)
                keys.Add(new KeyChord(Key.D0 + i, ModifierKeys.Control).ToString());
        foreach (var (letter, t) in Letters)
            if (t == tool) keys.Add(new KeyChord(letter, ModifierKeys.None).ToString());
        return string.Join(", ", keys);
    }
}
