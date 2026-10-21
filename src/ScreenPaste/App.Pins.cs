using System.Windows;
using ScreenPaste.Native;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenPaste;

/// <summary>
/// The hotkeys for pins: paste (Snipaste's F3) pins what the clipboard holds near the cursor,
/// or, with the clipboard unchanged since the last paste or the last close, brings back the
/// pin closed last (<see cref="PasteDecision"/>); click-through acts on the pin under the cursor. Hiding and
/// showing every pin is <see cref="PinManager.ToggleAllVisible"/>.
/// </summary>
public partial class App
{
    private const double DefaultDpi = 96.0;
    private uint? _pasteSequence;   // the clipboard's sequence number at the last paste

    /// <summary>F3, and the tray's Paste.</summary>
    private void PasteOrRestore()
    {
        uint sequence = NativeMethods.GetClipboardSequenceNumber();
        if (PasteDecision.Decide(sequence, _pasteSequence, PinManager.ClipboardSequenceAtLastClose, PinManager.ClosedCount) == PasteChoice.Restore &&
            PinManager.RestoreLastClosed())
            return;
        PasteClipboard(sequence);
    }

    /// <summary>A pin of the clipboard with its top-left at the cursor, kept on that monitor's
    /// work area. A text pin is drawn at that monitor's scale.</summary>
    private void PasteClipboard(uint sequence)
    {
        _pasteSequence = sequence;
        var cursor = NativeMethods.GetCursorPos(out var pt) ? pt : new NativeMethods.POINT();
        if (ClipboardPinSource.FromClipboard(MonitorScale(cursor)) is not { } content)
        {
            _tray?.ShowBalloonTip(2000, "ScreenPaste", Loc.T("paste.nothing"), Forms.ToolTipIcon.Info);
            return;
        }

        var area = Forms.Screen.FromPoint(new Drawing.Point(cursor.X, cursor.Y)).WorkingArea;
        var at = PinGeometry.PasteAt(new Point(cursor.X, cursor.Y), content.Image.PixelWidth, content.Image.PixelHeight,
            new Rect(area.X, area.Y, area.Width, area.Height));
        PinManager.Show(new PinWindow(content, (int)Math.Round(at.X), (int)Math.Round(at.Y), _settings, PinTransformState.Identity));
    }

    /// <summary>Physical px per DIP on the monitor at <paramref name="pt"/>; 1 when it cannot be read.</summary>
    private static double MonitorScale(NativeMethods.POINT pt)
    {
        try
        {
            var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0
                ? dpi / DefaultDpi
                : 1.0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return 1.0;   // shcore.dll missing: draw at 100%
        }
    }

    /// <summary>The click-through hotkey, at the cursor.</summary>
    private static void ToggleClickThroughAtCursor()
    {
        if (NativeMethods.GetCursorPos(out var pt)) PinManager.ToggleClickThroughAt(new Point(pt.X, pt.Y));
    }
}
