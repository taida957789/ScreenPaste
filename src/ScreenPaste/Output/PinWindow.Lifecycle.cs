using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>
/// How a pin comes and goes (Snipaste's close-versus-destroy model). Every close but a
/// destroy is recoverable: Esc, Ctrl+W, a double-click, the menu's Close, the global Esc,
/// even Alt+F4 hand <see cref="PinManager"/> a <see cref="ClosedPin"/> as the window goes,
/// and the paste hotkey can open it again where it was. Shift+Esc and the menu's Destroy skip
/// that. An image file dropped on the pin replaces what it shows; with Shift it opens a new
/// pin at the drop point.
/// </summary>
public sealed partial class PinWindow
{
    private PinCloseKind _closeKind = PinCloseKind.Recoverable;
    private bool _askingDoubleClick;           // the first double-click's question is on its way

    private void InitLifecycle()
    {
        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        Drop += OnDrop;
        Closing += OnClosing;
    }

    /// <summary>A closed pin, opened again as and where it was.</summary>
    internal static PinWindow Reopen(ClosedPin closed) =>
        new(closed.Content, closed.Left, closed.Top, closed.Settings, closed.State);

    /// <summary>Close the window as <paramref name="kind"/> (<see cref="PinManager.CloseRecoverable"/>,
    /// <see cref="PinManager.Destroy"/>).</summary>
    internal void CloseAs(PinCloseKind kind)
    {
        if (_closed) return;
        _closeKind = kind;
        Close();
    }

    /// <summary>The window still exists here, so where the pin is can be read back. A
    /// thumbnail comes back as the whole pin, where it would have gone.</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel) return;
        var at = FullImageTopLeft();
        var closed = new ClosedPin(_content, (int)Math.Round(at.X), (int)Math.Round(at.Y), _state, _settings);
        PinManager.Remember(closed, _closeKind);
    }

    /// <summary>
    /// Show <paramref name="content"/> instead (a dropped file; the clipboard). The turn,
    /// flips, zoom, opacity and effects stay, and so does the image's top-left, while the
    /// window takes the new image's size. A thumbnail of the old image ends first.
    /// </summary>
    public void ReplaceContent(PinContent content)
    {
        if (_closed) return;
        ExitThumbnail();
        var topLeft = ImageRect().TopLeft;
        _content = content;
        _effected = null;   // made from the image that is going
        _image.Width = content.Image.PixelWidth;
        _image.Height = content.Image.PixelHeight;
        Present(_state, topLeft);
    }

    // ------------------------------------------------------- double-click ---
    // A press is dispatched in PinWindow.Drag.cs: a plain double-click lands here; with Shift
    // it toggles the thumbnail, with Ctrl each click picks the pin or drops it, and with any
    // other modifier it is two drags, as it always was.

    private bool DoubleClickCloses =>
        PinDoubleClickActions.Parse(_settings.PinDoubleClickAction) == PinDoubleClickAction.Close;

    /// <summary>Close recoverably, with every pin picked with this one as Esc does, or false
    /// when a double-click does nothing. The first time it says so and asks, once this click is over.</summary>
    private bool OnDoubleClick()
    {
        if (!DoubleClickCloses) return false;
        if (_settings.PinDoubleClickTipShown) ForGroup(PinManager.CloseRecoverable);
        else if (!_askingDoubleClick)
        {
            _askingDoubleClick = true;
            Dispatcher.BeginInvoke(AskFirstDoubleClick);
        }
        return true;
    }

    /// <summary>The one-time tip: a double-click closes the pin, it can be brought back, and
    /// the menu turns this off. Asked once whatever the answer.</summary>
    private void AskFirstDoubleClick()
    {
        _askingDoubleClick = false;
        if (_closed) return;
        var answer = MessageBoxResult.None;
        PinManager.ShowingDialog(() => answer = MessageBox.Show(this, Loc.T("pin.dblClickMsg"),
            Loc.T("pin.dblClickTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question));
        _settings.PinDoubleClickTipShown = true;
        _settings.Save();
        if (answer == MessageBoxResult.Yes) ForGroup(PinManager.CloseRecoverable);
    }

    /// <summary>The menu's "Close on double-click": saved at once, and no tip is needed after.</summary>
    private void ToggleDoubleClickClose()
    {
        var next = DoubleClickCloses ? PinDoubleClickAction.None : PinDoubleClickAction.Close;
        _settings.PinDoubleClickAction = next.ToToken();
        _settings.PinDoubleClickTipShown = true;
        _settings.Save();
    }

    // -------------------------------------------------------------- drop ---

    /// <summary>Files are taken as a copy, whatever is held: a Shift-drag from Explorer
    /// would otherwise move them, deleting the originals.</summary>
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasFiles(e.Data) ? DragDropEffects.Copy & e.AllowedEffects : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Taken in at once, loaded once the drop is over, so the app the files come
    /// from is not kept waiting on a large image.</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var files = DroppedFiles(e.Data);
        e.Effects = files != null ? DragDropEffects.Copy & e.AllowedEffects : DragDropEffects.None;
        if (files == null) return;

        var modifiers = PinDrop.ModifiersOf(e.KeyStates);
        Point? dropAt = NativeMethods.GetCursorPos(out var pt) ? new Point(pt.X, pt.Y) : null;
        Dispatcher.BeginInvoke(() => ApplyDrop(files, modifiers, dropAt));
    }

    // A source that cannot hand its data over reads as no files.
    private static bool HasFiles(IDataObject data)
    {
        try { return data.GetDataPresent(DataFormats.FileDrop); }
        catch (COMException) { return false; }
    }

    private static string[]? DroppedFiles(IDataObject data)
    {
        try { return HasFiles(data) ? data.GetData(DataFormats.FileDrop) as string[] : null; }
        catch (COMException) { return null; }
    }

    private void ApplyDrop(string[] files, ModifierKeys modifiers, Point? dropAt)
    {
        if (_closed) return;
        var image = PinDrop.FirstLoadable(files, ImageLoader.TryLoad);
        var action = PinDrop.Decide(modifiers, image != null);
        if (image == null || action == PinDropAction.Reject)
        {
            Warn(Loc.T("img.loadFail"));
            return;
        }
        if (action == PinDropAction.NewPin) OpenPin(image, dropAt ?? ImageRect().TopLeft);
        else ReplaceContent(new PinContent(image));
    }

    /// <summary>Shift+drop: a new pin with its top-left at <paramref name="at"/> (physical screen px).</summary>
    private void OpenPin(BitmapSource image, Point at) =>
        new PinWindow(image, (int)Math.Round(at.X), (int)Math.Round(at.Y), _settings).Show();
}
