using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ScreenPaste.Editor;

/// <summary>
/// Reads a colour value from text, as the paste hotkey turns one on the clipboard into a
/// colour card (Snipaste): #RGB, #RRGGBB, #AARRGGBB, RGB(r, g, b), a bare "r, g, b", and
/// the loupe's own HSV(h, s%, v%) (<see cref="ColorFormat"/>), in any case and spacing.
/// The whole text must be the value, so a sentence that mentions a colour stays text.
/// </summary>
public static partial class ColorText
{
    private const int MaxHue = 360;
    private const int MaxPercent = 100;

    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.StartsWith('#')) return TryParseHex(s[1..], out color);

        var function = FunctionPattern().Match(s);
        if (function.Success)
        {
            var name = function.Groups["name"].Value.ToUpperInvariant();
            var args = function.Groups["args"].Value;
            return name == "RGB" ? TryParseRgb(args, out color) : TryParseHsv(args, out color);
        }
        return TryParseRgb(s, out color);
    }

    /// <summary>3, 6 or 8 hex digits after the '#'.</summary>
    private static bool TryParseHex(string digits, out Color color)
    {
        color = default;
        if (digits.Length is not (3 or 6 or 8) || !digits.All(char.IsAsciiHexDigit)) return false;
        if (digits.Length == 3) digits = string.Concat(digits.Select(c => new string(c, 2)));
        if (digits.Length == 6) digits = "FF" + digits;
        uint argb = uint.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }

    /// <summary>"r, g, b": three whole numbers 0-255.</summary>
    private static bool TryParseRgb(string args, out Color color)
    {
        color = default;
        if (Components(args, 3) is not { } parts) return false;
        var values = new byte[3];
        for (int i = 0; i < 3; i++)
        {
            if (!byte.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i])) return false;
        }
        color = Color.FromRgb(values[0], values[1], values[2]);
        return true;
    }

    /// <summary>"h, s%, v%": a hue in whole degrees below 360, then saturation and value in
    /// whole percent (the % signs may be left out).</summary>
    private static bool TryParseHsv(string args, out Color color)
    {
        color = default;
        if (Components(args, 3) is not { } parts) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int h) || h >= MaxHue) return false;
        if (!TryPercent(parts[1], out int sat) || !TryPercent(parts[2], out int val)) return false;
        color = FromHsv(h, sat / (double)MaxPercent, val / (double)MaxPercent);
        return true;
    }

    private static bool TryPercent(string part, out int value)
    {
        var digits = part.EndsWith('%') ? part[..^1].TrimEnd() : part;
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= MaxPercent;
    }

    /// <summary>Exactly <paramref name="count"/> comma-separated, trimmed, non-empty parts.</summary>
    private static string[]? Components(string args, int count)
    {
        var parts = args.Split(',').Select(p => p.Trim()).ToArray();
        return parts.Length == count && parts.All(p => p.Length > 0) ? parts : null;
    }

    private static Color FromHsv(int hue, double saturation, double value)
    {
        double c = value * saturation;
        double x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        double m = value - c;
        var (r, g, b) = (hue / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255);

    [GeneratedRegex(@"^(?<name>rgb|hsv)\s*\((?<args>[^()]*)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionPattern();
}
