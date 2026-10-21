using System.Windows;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// A custom snip (Snipaste's Shift+F1): the area to snip in physical screen px, and how
/// many seconds to wait first (so a menu or a hover state can be opened). Pure, so the
/// clamping is unit tested; CustomSnipDialog builds one and App runs it.
/// </summary>
public sealed record CustomSnipRequest(Int32Rect Region, double DelaySeconds)
{
    public const double MaxDelaySeconds = 60;

    /// <summary>The part of <paramref name="requested"/> that is on <paramref name="screen"/>,
    /// with the delay clamped to 0-60 seconds; null when the area has no size or lies
    /// entirely off screen.</summary>
    public static CustomSnipRequest? Validate(Int32Rect requested, double delaySeconds, VirtualScreen screen)
    {
        if (requested.Width <= 0 || requested.Height <= 0) return null;

        long left = Math.Max(requested.X, screen.X);
        long top = Math.Max(requested.Y, screen.Y);
        long right = Math.Min((long)requested.X + requested.Width, (long)screen.X + screen.Width);
        long bottom = Math.Min((long)requested.Y + requested.Height, (long)screen.Y + screen.Height);
        if (right <= left || bottom <= top) return null;

        var region = new Int32Rect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        return new CustomSnipRequest(region, ClampDelay(delaySeconds));
    }

    /// <summary>0-60 seconds; anything unreadable (NaN) is no delay.</summary>
    public static double ClampDelay(double seconds) =>
        double.IsNaN(seconds) ? 0 : Math.Clamp(seconds, 0, MaxDelaySeconds);
}
