using System.Windows;

namespace ScreenPaste.Native;

/// <summary>
/// The monitors' bounds in overlay-local physical px (the virtual screen's origin at 0,0),
/// for Ctrl+A in the capture overlay. The app is Per-Monitor-V2 aware, so the bounds
/// WinForms reports are physical px, the same space as the frozen screenshot.
/// </summary>
internal static class MonitorRects
{
    public static IReadOnlyList<Rect> Get(VirtualScreen vs) =>
        System.Windows.Forms.Screen.AllScreens
            .Select(s => new Rect(s.Bounds.X - vs.X, s.Bounds.Y - vs.Y, s.Bounds.Width, s.Bounds.Height))
            .ToList();
}
