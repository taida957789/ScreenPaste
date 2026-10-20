using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenPaste.Editor;
using ScreenPaste.Output;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The capture's outputs. Copy, save, quick save, pin, print and copy as a file flatten the
/// annotations onto the area and then leave the capture (not when a save or print dialog is
/// cancelled, or the file could not be copied); V only copies the area's size. Enter, a
/// double-click on the area with no tool and a middle-click inside it run the configurable
/// <see cref="OutputAction"/>s (Snipaste: copy, copy, pin), Shift+click on Save quick-saves,
/// and Space shows or hides the toolbar.
///
/// Split out of the main code-behind; which key does what is <see cref="OverlayKeyRouter"/>'s.
/// </summary>
public partial class CaptureOverlayWindow
{
    private static readonly TimeSpan SizeFlashDuration = TimeSpan.FromMilliseconds(1200);
    private DispatcherTimer? _sizeFlash;              // V: how long the chip says "copied"

    private void InitOutput()
    {
        PreviewMouseDown += Overlay_PreviewMiddleDown;
        Closed += (_, _) => _sizeFlash?.Stop();
    }

    private OutputAction EnterAction => OutputActions.Parse(_settings.EnterAction, OutputAction.Copy);
    private OutputAction DoubleClickAction => OutputActions.Parse(_settings.DoubleClickAction, OutputAction.Copy);
    private OutputAction MiddleClickAction => OutputActions.Parse(_settings.MiddleClickAction, OutputAction.Pin);

    // -------------------------------------------------------------- toolbar ---

    /// <summary>The toolbar's output buttons: copy, save (Shift+click: quick save), pin, close.</summary>
    private void AddOutputButtons(Panel row)
    {
        row.Children.Add(MakeActionButton(GlyphCopy, WithHotkey(Loc.T("action.copy"), _settings.CopyHotkey), DoCopy));
        row.Children.Add(MakeActionButton(GlyphSave,
            WithHotkey(Loc.T("action.save"), _settings.SaveHotkey) + " · " + Loc.T("action.shiftQuickSave"), SaveClicked));
        row.Children.Add(MakeActionButton(GlyphPin, WithHotkey(Loc.T("action.pin"), _settings.PinHotkey), DoPin));
        row.Children.Add(MakeActionButton(GlyphClose, Loc.T("action.close") + " (Esc)", Cancel));
    }

    /// <summary>"Copy (Ctrl+C)", or the label alone when the gesture is unbound.</summary>
    private static string WithHotkey(string label, string? hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? label : $"{label} ({hotkey.Trim()})";

    /// <summary>The Save button; Shift+click saves straight into the default folder.</summary>
    private void SaveClicked()
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) DoQuickSave();
        else DoSave();
    }

    /// <summary>Space: hide the toolbar to see what it covers, or bring it back below the area.</summary>
    private void ToggleToolbar()
    {
        if (Toolbar.Visibility != Visibility.Visible)
        {
            Toolbar.Visibility = Visibility.Visible;
            PositionToolbar();
            return;
        }
        // A toolbar control holding the keyboard focus is about to go; keep the keys here.
        if (Toolbar.IsKeyboardFocusWithin) Focus();
        Toolbar.Visibility = Visibility.Collapsed;
    }

    // -------------------------------------------------------- mouse actions ---

    /// <summary>A press on the area with no tool: a double-click runs the double-click action,
    /// anything else (or a double-click turned off) starts moving the region.</summary>
    private void Interior_MouseDown(MouseButtonEventArgs e)
    {
        var action = DoubleClickAction;
        if (e.ClickCount == 2 && action != OutputAction.None)
        {
            if (_regionMoving) Region_MoveEnd();   // the first click's move, should it linger
            RunOutputAction(action);
            e.Handled = true;
            return;
        }
        Region_MoveStart(e.GetPosition(RootCanvas));
    }

    /// <summary>A middle-click inside the area runs the middle-click action. Left alone on the
    /// toolbar and its font list, while typing (the text box owns it) and while the left
    /// button holds a drag.</summary>
    private void Overlay_PreviewMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _phase != Phase.Editing) return;
        if (_editingText != null || e.LeftButton == MouseButtonState.Pressed) return;
        if (_fontCombo?.IsDropDownOpen == true) return;
        if (e.OriginalSource is DependencyObject src && IsWithin(src, Toolbar)) return;
        if (!RegionOnCanvas().Contains(e.GetPosition(RootCanvas))) return;
        if (RunOutputAction(MiddleClickAction)) e.Handled = true;
    }

    /// <summary>Run a configurable action; false when it is turned off.</summary>
    private bool RunOutputAction(OutputAction action)
    {
        switch (action)
        {
            case OutputAction.Copy: DoCopy(); return true;
            case OutputAction.Pin: DoPin(); return true;
            case OutputAction.Save: DoSave(); return true;
            case OutputAction.QuickSave: DoQuickSave(); return true;
            case OutputAction.CopyAndPin: DoCopyAndPin(); return true;
            default: return false;
        }
    }

    // ----------------------------------------------------------- outputs ---

    private BitmapSource Flatten()
    {
        CommitActiveText(discardIfEmpty: true);
        FinishLineStrip();
        InvalidateSampledLayers();   // make sure every region reflects the final annotation stack
        // Re-sampling assigns new bitmaps to the blur/magnifier images, which invalidates
        // their layout; RenderTargetBitmap draws whatever was last *arranged*, so flush the
        // pending pass or a freshly sampled image would export at a stale (or zero) size.
        EditLayer.UpdateLayout();
        return Compositor.Compose(_screenshot, _selection, Ink.Strokes, BlurHost, MagnifyHost,
            ShapeHost, StickerHost, TextHost);
    }

    /// <summary>Flatten, hand the image to <paramref name="output"/>, and leave the capture
    /// once it reports success. False when it did not (a cancelled dialog).</summary>
    private bool Export(Func<BitmapSource, bool> output)
    {
        if (!output(Flatten())) return false;
        PersistSettings();
        Close();
        return true;
    }

    // Copy leaves even if another app kept the clipboard locked, as it always has.
    private void DoCopy() => Export(img => { ClipboardService.CopyImage(img); return true; });

    private void DoSave()
    {
        if (!Export(img => FileSaveService.SaveAs(img, _settings.SaveDirectory) != null))
            SelectionBorder.Visibility = Visibility.Visible;
    }

    private void DoQuickSave() => Export(img => { FileSaveService.QuickSave(img, _settings.SaveDirectory); return true; });

    private void DoPin() => Export(img => { PinImage(img); return true; });

    private void DoCopyAndPin() => Export(img => { ClipboardService.CopyImage(img); PinImage(img); return true; });

    /// <summary>Ctrl+Alt+C: copy as a PNG file, for Explorer or an upload box.</summary>
    private void DoCopyAsFile()
    {
        if (!Export(ClipboardService.CopyAsFile))
            MessageBox.Show(this, Loc.T("copyFile.failed"), "ScreenPaste", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// Ctrl+P: print on one page, then leave; a cancelled dialog keeps editing. Once a printer
    /// is chosen the overlay hides, so a printer's own dialog (a PDF printer asking for a file
    /// name) cannot open behind this topmost full-screen window, and it comes back if the job
    /// is cancelled there or fails.
    /// </summary>
    private void DoPrint()
    {
        var img = Flatten();
        bool printed;
        try
        {
            printed = PrintService.Print(img, Loc.T("app.name"), beforePrinting: Hide);
        }
        // Printer drivers fail in many ways (spooler, XPS, Win32, COM). Whatever it was, the
        // overlay may be hidden for them: bring it back with the capture still being edited.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ShowAgain();
            MessageBox.Show(this, Loc.T("print.failed", ex.Message), "ScreenPaste", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!printed)
        {
            ShowAgain();
            return;
        }
        PersistSettings();
        Close();
    }

    /// <summary>Back from hiding for a printer's dialog, with the keyboard.</summary>
    private void ShowAgain()
    {
        if (IsVisible) return;
        Show();
        Activate();
        Focus();
    }

    /// <summary>A pin of the image, over the spot it was captured from.</summary>
    private void PinImage(BitmapSource img) =>
        new PinWindow(img, _vs.X + _selection.X, _vs.Y + _selection.Y, _settings.SaveDirectory).Show();

    /// <summary>V: copy the area's size ("800x600") and say on the size chip for a moment
    /// whether it worked (another app can keep the clipboard locked).</summary>
    private void CopySize()
    {
        bool copied = ClipboardService.CopyText(OutputActions.SizeText(_selection.Width, _selection.Height));
        FlashSizeNote(Loc.T(copied ? "size.copied" : "size.copyFailed"));
    }

    /// <summary>Show <paramref name="note"/> after the size on the chip for a moment (a copied
    /// size, a new pen width ...), then the size alone again.</summary>
    private void FlashSizeNote(string note)
    {
        ShowSizeReadout(RegionOnCanvas(), note);
        _sizeFlash ??= new DispatcherTimer(SizeFlashDuration, DispatcherPriority.Normal, (_, _) => EndSizeFlash(), Dispatcher);
        _sizeFlash.Stop();   // restart: a second note stays up for the full time
        _sizeFlash.Start();
    }

    private void EndSizeFlash()
    {
        _sizeFlash?.Stop();
        if (_phase == Phase.Editing) ShowSizeReadout(RegionOnCanvas());
    }

    /// <summary>
    /// Leave the capture from the editing phase: always ask first (a selected region
    /// counts as work too), with a "don't ask again" option persisted to settings.
    /// </summary>
    private void ConfirmCancel()
    {
        CommitActiveText(discardIfEmpty: true);
        if (ConfirmDiscard()) Cancel();
    }

    private void Cancel() => Close();

    private void PersistSettings()
    {
        _settings.PenWidth = _penWidth;
        _settings.PenOpacity = _penOpacity;
        _settings.PenColor = ToHex(_penColor);
        _settings.HighlighterWidth = _hlWidth;
        _settings.HighlighterOpacity = _hlOpacity;
        _settings.HighlighterColor = ToHex(_hlColor);
        _settings.BlurKind = _blurKind.ToString();
        _settings.GaussianStrength = _blurStrength;
        _settings.TextFont = _textFont;
        _settings.TextSize = _textSize;
        _settings.TextColor = ToHex(_textColor);
        _settings.TextBold = _textBold;
        _settings.TextItalic = _textItalic;
        _settings.TextStrikethrough = _textStrike;
        _settings.ShapeKind = _shapeKind.ToString();
        _settings.ShapeFilled = _shapeFilled;
        _settings.ShapeColor = ToHex(_shapeColor);
        _settings.ShapeWidth = _shapeWidth;
        _settings.NumberSize = _numberSize;
        _settings.EraserWidth = _eraserWidth;
        _settings.LineWidth = _lineWidth;
        _settings.LineColor = ToHex(_lineColor);
        _settings.LineArrowStart = _lineArrowStart;
        _settings.LineArrowEnd = _lineArrowEnd;
        _settings.MagnifyShape = _magnifyShape.ToString();
        _settings.MagnifyZoom = _magnifyZoom;
        _settings.MagnifyBorderWidth = _magnifyBorderWidth;
        _settings.MagnifyBorderColor = ToHex(_magnifyBorderColor);
        _settings.MagnifyConnector = _magnifyConnector;
        _settings.MagnifyShadow = _magnifyShadow;
        _settings.MagnifySmooth = _magnifySmooth;
        _settings.MagnifyIncludeAnnotations = _magnifyWithAnnotations;
        _settings.CustomColors = _customColors.Select(ToHex).ToList();
        _settings.Save();
    }

    /// <summary>The chosen region in RootCanvas space (overlay-local physical px).</summary>
    private Rect RegionOnCanvas() => new(_selection.X, _selection.Y, _selection.Width, _selection.Height);
}
