using System.Windows;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// What a capture overlay starts from: the frozen screenshot and the screen layout it covers,
/// the pointer as it was when the capture began, and the state that outlives one capture,
/// which App owns for its lifetime: the areas (R / Shift+R) and the snips (, / .) of earlier
/// successful snips. A snip mode (Ctrl+F1 and the extra hotkeys) can choose the area up front
/// and finish the capture once it is chosen.
/// </summary>
public sealed record CaptureRequest
{
    public required BitmapSource Screenshot { get; init; }
    public required VirtualScreen Screen { get; init; }
    /// <summary>Null when the pointer was hidden, or could not be captured.</summary>
    public CursorSnapshot? Cursor { get; init; }
    public required SnipRegionHistory Regions { get; init; }
    public required SnipRecordStore<SnipRecord> Records { get; init; }
    /// <summary>How the capture finishes once its area is chosen (<see cref="CaptureModes.FinishAction"/>).</summary>
    public CaptureMode Mode { get; init; }
    /// <summary>The area to choose as soon as the overlay is up, in physical screen px (a
    /// custom snip, the last area, the active window ...); null to frame it by hand.</summary>
    public Rect? PresetRegion { get; init; }
}

/// <summary>The ways a capture can start (the global hotkeys).</summary>
public enum CaptureMode
{
    Normal,           // frame, then annotate
    Copy,             // Ctrl+F1: the chosen area goes straight to the clipboard
    Pin,              // the chosen area is pinned at once
    CopyAndPin,       // both
    LastAreaCopy,     // the last snip's area, copied at once
    FullScreenCopy,   // the whole screen, copied at once
    ActiveWindow,     // the foreground window's area chosen, then annotate
    ActiveScreen,     // the monitor under the cursor chosen, then annotate
}

public static class CaptureModes
{
    /// <summary>What the overlay runs as soon as the area is chosen; None leaves it to the user.</summary>
    public static OutputAction FinishAction(CaptureMode mode) => mode switch
    {
        CaptureMode.Copy or CaptureMode.LastAreaCopy or CaptureMode.FullScreenCopy => OutputAction.Copy,
        CaptureMode.Pin => OutputAction.Pin,
        CaptureMode.CopyAndPin => OutputAction.CopyAndPin,
        _ => OutputAction.None,
    };
}
