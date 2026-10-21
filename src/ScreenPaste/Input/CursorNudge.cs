using System.Windows.Input;
using ScreenPaste.Native;

namespace ScreenPaste.Input;

/// <summary>
/// Snipaste-style keyboard cursor control: W/A/S/D (and the arrows while framing) move the
/// OS cursor by one pixel, accelerating while the key auto-repeats. Holding the left button
/// at the same time gives pixel-exact dragging, since every drag follows the cursor.
/// </summary>
public static class CursorNudge
{
    /// <summary>The largest step, in physical px, a held key reaches.</summary>
    public const int MaxStep = 8;

    /// <summary>The step for the <paramref name="repeatCount"/>-th auto-repeat of the key
    /// (0 = the press itself): 1, 1, 1, 2, 4, 8, 8 ... px.</summary>
    public static int Step(int repeatCount)
    {
        if (repeatCount < 3) return 1;
        int doublings = repeatCount - 2;
        return doublings >= 3 ? MaxStep : Math.Min(MaxStep, 1 << doublings);
    }

    /// <summary>The unit step a key stands for; (0, 0) for any other key.</summary>
    public static (int Dx, int Dy) Direction(Key key) => key switch
    {
        Key.W or Key.Up => (0, -1),
        Key.A or Key.Left => (-1, 0),
        Key.S or Key.Down => (0, 1),
        Key.D or Key.Right => (1, 0),
        _ => (0, 0),
    };

    /// <summary>Keep a physical screen point on the virtual desktop's last pixel row and
    /// column; the origin can be negative when a monitor sits left of or above the primary.</summary>
    public static (int X, int Y) Clamp(int x, int y, VirtualScreen screen) => (
        Math.Clamp(x, screen.X, screen.X + Math.Max(1, screen.Width) - 1),
        Math.Clamp(y, screen.Y, screen.Y + Math.Max(1, screen.Height) - 1));
}
