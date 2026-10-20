using System.Windows.Media;

namespace ScreenPaste.Editor;

/// <summary>How the loupe writes a pixel colour (a Shift tap cycles through them).</summary>
public enum ColorFormatKind
{
    Hex,   // #3DA9FC
    Rgb,   // RGB(61, 169, 252)
    Hsv,   // HSV(206, 76%, 99%)
}

/// <summary>
/// The loupe's colour text. The C key copies exactly what this produces, so the readout
/// and the clipboard never disagree. Alpha is ignored: screen pixels are opaque.
/// </summary>
public static class ColorFormat
{
    public static ColorFormatKind Next(ColorFormatKind kind) => kind switch
    {
        ColorFormatKind.Hex => ColorFormatKind.Rgb,
        ColorFormatKind.Rgb => ColorFormatKind.Hsv,
        _ => ColorFormatKind.Hex,
    };

    public static string Format(Color c, ColorFormatKind kind) => kind switch
    {
        ColorFormatKind.Rgb => FormattableString.Invariant($"RGB({c.R}, {c.G}, {c.B})"),
        ColorFormatKind.Hsv => FormatHsv(c),
        _ => FormattableString.Invariant($"#{c.R:X2}{c.G:X2}{c.B:X2}"),
    };

    /// <summary>Hue in whole degrees (0-359), saturation and value in whole percent.</summary>
    private static string FormatHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * ((((g - b) / delta) % 6 + 6) % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }

        int h = (int)Math.Round(hue) % 360;   // 359.8 rounds to 360, which is 0
        int s = (int)Math.Round(max == 0 ? 0 : delta / max * 100);
        int v = (int)Math.Round(max * 100);
        return FormattableString.Invariant($"HSV({h}, {s}%, {v}%)");
    }
}
