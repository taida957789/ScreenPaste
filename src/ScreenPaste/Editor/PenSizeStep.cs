namespace ScreenPaste.Editor;

/// <summary>The values a tool's width slider allows, and how far one wheel notch or key moves it.</summary>
public readonly record struct PenSizeRange(double Min, double Max, double Step);

/// <summary>
/// The wheel, 1 / 2 and [ / ] make the current tool thinner or thicker: the pen width, the
/// font size for text, the strength for blur, the border for a magnifier, the circle for a
/// number marker and the eraser's size, each within its toolbar slider's range. The wheel on a colour swatch steps the colour's opacity the same
/// way. A value the slider was dragged to lands on the step grid on its first step.
/// </summary>
public static class PenSizeStep
{
    /// <summary>The pen opacity slider's range, in 5% steps; colours with an alpha share it.</summary>
    public static readonly PenSizeRange OpacityRange = new(0.1, 1.0, 0.05);

    // How close (in steps) a value must be to a grid point to count as on it: absorbs a
    // colour's alpha being a byte (45% is stored as 115/255) and floating-point noise.
    private const double GridTolerance = 0.05;
    private const int Decimals = 6;

    /// <summary>The range the tool's width slider allows, or null for a tool without one.</summary>
    public static PenSizeRange? RangeOf(ToolKind tool) => tool switch
    {
        ToolKind.MarkerPen or ToolKind.Highlighter => new PenSizeRange(1, 40, 1),
        ToolKind.Shape or ToolKind.Line => new PenSizeRange(1, 20, 1),
        ToolKind.Text => new PenSizeRange(10, 96, 2),
        ToolKind.Blur => new PenSizeRange(2, 40, 2),
        ToolKind.Magnify => new PenSizeRange(0, 12, 1),
        ToolKind.Number => new PenSizeRange(16, 64, 2),
        ToolKind.Eraser => new PenSizeRange(4, 60, 2),
        _ => null,
    };

    /// <summary>The tool's width one step in <paramref name="direction"/> (its sign) from
    /// <paramref name="value"/>; unchanged for a tool without a width.</summary>
    public static double Next(ToolKind tool, double value, int direction) =>
        RangeOf(tool) is { } range ? Next(range, value, direction) : value;

    /// <summary>The opacity one 5% step in <paramref name="direction"/> from <paramref name="opacity"/>.</summary>
    public static double NextOpacity(double opacity, int direction) => Next(OpacityRange, opacity, direction);

    /// <summary>The next grid point (a multiple of the step) past <paramref name="value"/> in
    /// <paramref name="direction"/>, held inside the range.</summary>
    public static double Next(PenSizeRange range, double value, int direction)
    {
        if (direction == 0 || range.Step <= 0) return value;

        double q = value / range.Step;
        double nearest = Math.Round(q);
        if (Math.Abs(q - nearest) < GridTolerance) q = nearest;

        double next = direction > 0 ? Math.Floor(q) + 1 : Math.Ceiling(q) - 1;
        return Math.Round(Math.Clamp(next * range.Step, range.Min, range.Max), Decimals);
    }
}
