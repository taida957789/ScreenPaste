using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenPaste.Editor;

/// <summary>What a number marker shows and how big it is; parked on the marker's Tag.</summary>
public sealed record NumberMarkerSpec(int Number, double Diameter, Color Fill);

/// <summary>
/// The number-marker annotation: a filled circle centred on the click with its number in the
/// middle, white or black, whichever reads better on the circle's colour. The look is pure
/// (<see cref="Frame"/>, <see cref="FontSizeFor"/>, <see cref="TextColorFor"/>), so it is unit
/// tested; <see cref="Create"/> builds the element from a spec, which also lets it be cloned.
/// </summary>
public static class NumberMarker
{
    // Relative luminance above which black text reads better than white. Lower than WCAG's
    // crossover on purpose: white on the default red is what Snipaste and most tools show.
    private const double LightFill = 0.5;

    /// <summary>The circle's bounds for a click at <paramref name="center"/>.</summary>
    public static Rect Frame(Point center, double diameter) =>
        new(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);

    /// <summary>The number's font size: smaller as it gets longer, so three digits still fit.</summary>
    public static double FontSizeFor(double diameter, int number)
    {
        int digits = Math.Abs(number).ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        double share = digits switch { <= 1 => 0.6, 2 => 0.5, _ => 0.4 };
        return diameter * share;
    }

    /// <summary>White on a dark or saturated fill, black on a light one; the fill's opacity is
    /// ignored.</summary>
    public static Color TextColorFor(Color fill)
    {
        double luminance = 0.2126 * Linear(fill.R) + 0.7152 * Linear(fill.G) + 0.0722 * Linear(fill.B);
        return luminance > LightFill ? Colors.Black : Colors.White;
    }

    /// <summary>The marker for <paramref name="spec"/>, centred on <paramref name="center"/>
    /// (region-local px).</summary>
    public static FrameworkElement Create(NumberMarkerSpec spec, Point center)
    {
        var marker = new Grid { Width = spec.Diameter, Height = spec.Diameter, Tag = spec };
        marker.Children.Add(new Ellipse { Fill = new SolidColorBrush(spec.Fill) });
        marker.Children.Add(new TextBlock
        {
            Text = spec.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = FontSizeFor(spec.Diameter, spec.Number),
            Foreground = new SolidColorBrush(TextColorFor(spec.Fill)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var frame = Frame(center, spec.Diameter);
        Canvas.SetLeft(marker, frame.X);
        Canvas.SetTop(marker, frame.Y);
        return marker;
    }

    public static NumberMarkerSpec? SpecOf(FrameworkElement element) =>
        element is Grid { Tag: NumberMarkerSpec spec } ? spec : null;

    /// <summary>Whether a press with <paramref name="tool"/> on an annotation picks it up to
    /// move it. The number tool is used on top of other annotations (it numbers what a frame
    /// encloses), so it only picks up another number marker and otherwise places its own;
    /// every other tool picks up whatever it is pressed on.</summary>
    public static bool PressGrabs(ToolKind tool, bool onNumberMarker) =>
        tool != ToolKind.Number || onNumberMarker;

    // sRGB channel to linear light (WCAG relative luminance).
    private static double Linear(byte channel)
    {
        double c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
