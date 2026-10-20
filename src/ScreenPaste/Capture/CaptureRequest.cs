using System.Windows.Media.Imaging;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// What a capture overlay starts from: the frozen screenshot and the screen layout it covers,
/// the pointer as it was when the capture began, and the state that outlives one capture,
/// which App owns for its lifetime: the areas (R / Shift+R) and the snips (, / .) of earlier
/// successful snips.
/// </summary>
public sealed record CaptureRequest
{
    public required BitmapSource Screenshot { get; init; }
    public required VirtualScreen Screen { get; init; }
    /// <summary>Null when the pointer was hidden, or could not be captured.</summary>
    public CursorSnapshot? Cursor { get; init; }
    public required SnipRegionHistory Regions { get; init; }
    public required SnipRecordStore<SnipRecord> Records { get; init; }
}
