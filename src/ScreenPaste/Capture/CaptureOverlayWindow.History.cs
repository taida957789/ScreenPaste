using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The snip context: what a capture knows beyond its own screenshot, handed over by App in the
/// <see cref="CaptureRequest"/>. ` or ! shows or hides the pointer as it was when the capture
/// began (remembered across captures); R / Shift+R step through the areas of the last
/// successful snips (<see cref="SnipRegionHistory"/>); , / . replay those snips, their
/// annotations flattened into the screenshot (<see cref="SnipRecordStore{T}"/>), and '.' past
/// the newest returns to this capture's own screen; F5 captures the screen afresh from under
/// the overlay, keeping the area and the annotations. Every successful output records the
/// snip for both histories.
///
/// Split out of the main code-behind; which key does what is <see cref="OverlayKeyRouter"/>'s.
/// </summary>
public partial class CaptureOverlayWindow
{
    private const long RefreshRepeatMs = 250;   // a held F5 refreshes about four times a second

    private readonly CaptureRequest _request;
    private BitmapSource _liveScreenshot = null!;   // this capture's own screen (F5 renews it)
    private CursorSnapshot? _cursor;                // the pointer of the screenshot shown
    private bool _showCursor;                       // AppSettings.SnipShowCursor
    private long _lastRefreshMs;

    private void InitHistory()
    {
        _liveScreenshot = _request.Screenshot;
        _showCursor = _settings.SnipShowCursor;
        ScreenImage.Width = _vs.Width;
        ScreenImage.Height = _vs.Height;
        // A new capture starts again from the newest area, on its own screen; a record of
        // another screen layout could not back this overlay.
        _request.Regions.Reset();
        _request.Records.RemoveAll(r => r.Screen != _vs);
        ShowBackdrop(_liveScreenshot, _request.Cursor);
    }

    /// <summary>Execute a snip-context command; false leaves the key unhandled.</summary>
    private bool RunSnipCommand(OverlayCommand command, OverlayKeyContext ctx)
    {
        switch (command)
        {
            case OverlayCommand.ToggleCursor:
                if (!ctx.IsRepeat) ToggleCursor();   // a held ` must not make it flicker
                return true;
            case OverlayCommand.PreviousRegion: ApplyRememberedRegion(_request.Regions.Previous()); return true;
            case OverlayCommand.NextRegion: ApplyRememberedRegion(_request.Regions.Next()); return true;
            case OverlayCommand.HistoryPrevious: StepSnipHistory(older: true); return true;
            case OverlayCommand.HistoryNext: StepSnipHistory(older: false); return true;
            case OverlayCommand.RefreshScreenshot: RefreshScreenshot(ctx.IsRepeat); return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------- backdrop ---

    /// <summary>Show <paramref name="screenshot"/> behind everything, with its pointer, and drop
    /// whatever was sampled from the previous one: the loupe's pixels, the blur and magnifier
    /// composites (the annotations then re-sample the new pixels).</summary>
    private void ShowBackdrop(BitmapSource screenshot, CursorSnapshot? cursor)
    {
        _screenshot = screenshot;
        _pixels = null;
        _cursor = cursor;
        ScreenImage.Source = screenshot;
        LayoutCursorImage();
        InvalidateSampledLayers();
        if (Magnifier.Visibility == Visibility.Visible) UpdateMagnifier(CursorLocal());
    }

    // -------------------------------------------------------------- pointer ---

    /// <summary>` or !: show or hide the captured pointer, remembered for later captures.</summary>
    private void ToggleCursor()
    {
        _showCursor = !_showCursor;
        _settings.SnipShowCursor = _showCursor;
        _settings.Save();
        LayoutCursorImage();
        InvalidateSampledLayers();   // a blur or magnifier over it shows it too
    }

    private void LayoutCursorImage()
    {
        if (ShownCursor() is not { } cursor)
        {
            CursorImage.Visibility = Visibility.Collapsed;
            return;
        }
        CursorImage.Source = cursor.Image;
        CursorImage.Width = cursor.Image.PixelWidth;   // RootCanvas units are physical px
        CursorImage.Height = cursor.Image.PixelHeight;
        Canvas.SetLeft(CursorImage, cursor.Position.X);
        Canvas.SetTop(CursorImage, cursor.Position.Y);
        CursorImage.Visibility = Visibility.Visible;
    }

    /// <summary>The pointer as the compositor draws it, or null while it is hidden.</summary>
    private CursorLayer? ShownCursor() =>
        _showCursor && _cursor is { } c ? new CursorLayer(c.Image, c.LocalTo(_vs)) : null;

    // ------------------------------------------------------------ last areas ---

    /// <summary>R / Shift+R: take a remembered area (kept on this screen). Framing, it is
    /// chosen; editing, the region moves there and the annotations stay with the content.</summary>
    private void ApplyRememberedRegion(Rect? remembered)
    {
        if (remembered is not { } screenRect) return;
        var local = SnipRegionHistory.FitInto(screenRect, _vs);
        if (_phase == Phase.Selecting) AcceptRegion(local);
        else ApplyRegion(Snap(local));
    }

    // ---------------------------------------------------------- snip history ---

    /// <summary>
    /// , / .: show the next older or newer snip, flattened into its screenshot with its area
    /// chosen, or, past the newest, this capture's own screen to frame afresh. Annotations made
    /// since go with it, so with some on the canvas this asks first, as choosing the area again
    /// does; declining stays where it was.
    /// </summary>
    private void StepSnipHistory(bool older)
    {
        var records = _request.Records;
        if (!(older ? records.MoveOlder() : records.MoveNewer())) return;

        if (_phase == Phase.Editing && HasAnnotations() &&
            !ConfirmDiscard(Loc.T("dlg.historyTitle"), Loc.T("dlg.historyMsg"), Loc.T("dlg.historyYes")))
        {
            if (older) records.MoveNewer();   // a step and its opposite always undo each other
            else records.MoveOlder();
            return;
        }
        ShowSnip(records.Current);
    }

    private void ShowSnip(SnipRecord? record)
    {
        if (_phase == Phase.Editing)
        {
            SelectTool(ToolKind.None);   // as a right-click drops it before choosing again
            ReturnToSelecting();         // drops the annotations and their history
        }
        if (record == null)
        {
            ShowBackdrop(_liveScreenshot, _request.Cursor);
            return;
        }
        ShowBackdrop(Compositor.PasteRegion(record.Screenshot, record.Annotated, record.Region), record.Cursor);
        var r = record.Region;
        AcceptRegion(new Rect(r.X, r.Y, r.Width, r.Height));
    }

    /// <summary>A successful output (copy, save, pin, print ...): remember its area for R and
    /// the snip for , / .. The record keeps the pointer apart from the flattened area, so a
    /// replay can show or hide it again. Nothing is built for a record the store would turn
    /// away (history off, or larger than its budget); the flattened area is as large as
    /// <paramref name="output"/>.</summary>
    private void RecordSnip(BitmapSource output)
    {
        _request.Regions.Push(new Rect(_vs.X + _selection.X, _vs.Y + _selection.Y, _selection.Width, _selection.Height));

        long bytes = SnipRecord.BytesOf(_screenshot.PixelWidth, _screenshot.PixelHeight) +
                     SnipRecord.BytesOf(output.PixelWidth, output.PixelHeight);
        if (!_request.Records.Accepts(bytes)) return;

        var annotated = output;
        if (ShownCursor() is { } c &&
            CursorPlacement.InRegion(c.Position, c.Image.PixelWidth, c.Image.PixelHeight, _selection) != null)
            annotated = ComposeWithoutCursor();
        var record = new SnipRecord(_screenshot, _vs, _selection, annotated, _cursor);
        _request.Records.Add(record, record.Bytes);
    }

    /// <summary>The area with every annotation but not the pointer. The blurs and magnifiers
    /// sampled it with the pointer, so they sample again without it first, or a replay with
    /// the pointer hidden would still show its outline inside them. Only on the way out: the
    /// layers are left without the pointer while CursorImage still shows it.</summary>
    private BitmapSource ComposeWithoutCursor()
    {
        bool shown = _showCursor;
        _showCursor = false;
        try
        {
            if (BlurHost.Children.Count > 0 || MagnifyHost.Children.Count > 0)
            {
                InvalidateSampledLayers();
                EditLayer.UpdateLayout();   // as Flatten: draw the freshly sampled bitmaps
            }
            return ComposeRegion(null);
        }
        finally
        {
            _showCursor = shown;
        }
    }

    // --------------------------------------------------------------- refresh ---

    /// <summary>F5: the screen as it is now replaces the screenshot, in either phase; the area
    /// and the annotations stay (they re-sample it), and a replayed snip gives way to the live
    /// screen. Held, it refreshes about four times a second. The pointer stays the one from the
    /// capture's start, since the one on screen now is the overlay's own.</summary>
    private void RefreshScreenshot(bool isRepeat)
    {
        long now = Environment.TickCount64;
        if (isRepeat && now - _lastRefreshMs < RefreshRepeatMs) return;
        _lastRefreshMs = now;

        if (CaptureFromUnderOverlay() is not { } fresh) return;
        _liveScreenshot = fresh;
        _request.Records.Reset();
        ShowBackdrop(fresh, _request.Cursor);
    }

    /// <summary>
    /// The screen without the overlay: excluded from screen capture for the moment (Windows 10
    /// 2004 and later), otherwise hidden while the shot is taken. Null when the screen layout no
    /// longer matches the overlay's, or the screen could not be read (a locked workstation).
    /// </summary>
    private BitmapSource? CaptureFromUnderOverlay()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        bool excluded = NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        if (!excluded) Hide();
        try
        {
            // The compositor leaves the overlay out from its next frame on; wait for it.
            NativeMethods.DwmFlush();
            NativeMethods.DwmFlush();
            var shot = ScreenCapture.CaptureVirtualScreen(out var screen);
            return screen == _vs ? shot : null;
        }
        catch (ExternalException)   // GDI could not read the screen (Win32Exception included)
        {
            return null;
        }
        finally
        {
            if (excluded) NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE);
            else ShowAgain();
        }
    }
}
