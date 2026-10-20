using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenPaste.Native;

namespace ScreenPaste.Output;

/// <summary>
/// Tracks all open pins, the pins closed recoverably, and the global-Esc policy:
///   • a focused pin closes itself (handled by the pin's own key handler);
///   • pressing Esc when NO pin is focused closes every shown pin at once (unless
///     AppSettings.PinGlobalEscClosesAll is off); hidden pins are left alone.
/// The second case needs a low-level keyboard hook, installed only while pins exist.
///
/// Snipaste's close-versus-destroy model: a pin closed any way but Destroy (Esc, Ctrl+W, a
/// double-click, the menu, the global Esc, Alt+F4) goes to <see cref="ClosedPins"/>, and
/// <see cref="RestoreLastClosed"/> opens the newest one again where it was.
///
/// The global hotkeys act on all pins here: <see cref="ToggleAllVisible"/> hides them all and
/// shows them again (hiding is not closing), <see cref="ToggleClickThroughAt"/> lets clicks
/// through the pin under the cursor.
/// </summary>
public static class PinManager
{
    private static readonly List<PinWindow> Pins = new();
    private static readonly ClosedPinHistory<ClosedPin> ClosedPins = new(0);   // sized on each close
    private static readonly HashSet<PinWindow> HiddenPins = new();              // put away by ToggleAllVisible
    private static IntPtr _hook = IntPtr.Zero;
    private static NativeMethods.HookProc? _proc;   // kept alive to avoid GC of the callback

    private static int _dialogDepth;   // pins' own dialogs open (save, print, a message)

    /// <summary>Set while the capture overlay is up, so Esc there won't wipe existing pins.</summary>
    public static bool CaptureActive { get; set; }

    /// <summary>Opens the settings window; App sets it. Lets a pin's Ctrl+Shift+P reach the
    /// settings (useful with the tray icon hidden) without the pin knowing App.</summary>
    public static Action? OpenSettingsRequested { get; set; }

    public static void RequestSettings() => OpenSettingsRequested?.Invoke();

    /// <summary>Run a pin's modal dialog. While it is up, Esc only cancels the dialog: no pin
    /// is focused then, which would otherwise read as "close every pin".</summary>
    public static void ShowingDialog(Action show)
    {
        _dialogDepth++;
        try { show(); }
        finally { _dialogDepth--; }
    }

    /// <summary>How many closed pins <see cref="RestoreLastClosed"/> can still bring back.</summary>
    public static int ClosedCount => ClosedPins.Count;

    /// <summary>Close <paramref name="pin"/> so it can be brought back.</summary>
    public static void CloseRecoverable(PinWindow pin) => pin.CloseAs(PinCloseKind.Recoverable);

    /// <summary>Close <paramref name="pin"/> for good: it skips the history, so nothing keeps its image.</summary>
    public static void Destroy(PinWindow pin) => pin.CloseAs(PinCloseKind.Destroy);

    /// <summary>Open the newest closed pin again, as and where it was. False when none is left.</summary>
    public static bool RestoreLastClosed()
    {
        if (ClosedPins.Pop() is not { } closed) return false;
        Show(PinWindow.Reopen(closed));
        return true;
    }

    /// <summary>Show a new or restored pin. While a capture is up the pin does not take the
    /// focus, so the overlay keeps the keyboard (Snipaste pastes while snipping).</summary>
    public static void Show(PinWindow pin)
    {
        pin.ShowActivated = !CaptureActive;
        pin.Show();
    }

    /// <summary>Shift+F3: hide every shown pin, or, with none shown, show the ones hidden this
    /// way again where they were. A hidden pin is not closed, and the global Esc leaves it be.</summary>
    public static void ToggleAllVisible()
    {
        var shown = Pins.Where(p => p.IsVisible).ToList();
        if (shown.Count > 0)
        {
            foreach (var pin in shown)
            {
                HiddenPins.Add(pin);
                pin.Hide();
            }
            return;
        }
        foreach (var pin in HiddenPins.ToList())
        {
            pin.ShowActivated = false;
            pin.Show();
        }
        HiddenPins.Clear();
    }

    /// <summary>
    /// The click-through hotkey: the shown pin under <paramref name="cursor"/> (physical
    /// screen px; the topmost, should several overlap) lets clicks through to the window
    /// beneath, or takes them again. With no pin there, every pin takes clicks again, so a
    /// pin made click-through can always be got back.
    /// </summary>
    public static void ToggleClickThroughAt(Point cursor)
    {
        var shown = PinsInZOrder().Where(p => p.IsVisible).ToList();
        if (PinHitTest.Find(shown.Select(p => p.ScreenRect()).ToList(), cursor) is int hit)
            shown[hit].SetClickThrough(!shown[hit].IsClickThrough);
        else
            MakeAllClickable();
    }

    /// <summary>The tray's escape hatch: every pin takes clicks again.</summary>
    public static void MakeAllClickable()
    {
        foreach (var pin in Pins) pin.SetClickThrough(false);
    }

    /// <summary>The open pins, topmost first (EnumWindows walks the top-level windows in Z order).</summary>
    private static List<PinWindow> PinsInZOrder()
    {
        var byHandle = new Dictionary<IntPtr, PinWindow>();
        foreach (var pin in Pins)
        {
            var handle = new WindowInteropHelper(pin).Handle;
            if (handle != IntPtr.Zero) byHandle[handle] = pin;   // not shown yet: not on screen either
        }
        var ordered = new List<PinWindow>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (byHandle.TryGetValue(hwnd, out var pin)) ordered.Add(pin);
            return true;
        }, IntPtr.Zero);
        return ordered;
    }

    /// <summary>A pin is closing (the pin calls this while its window still exists): keep it
    /// unless it is being destroyed, within the capacity its settings give now.</summary>
    internal static void Remember(ClosedPin closed, PinCloseKind kind)
    {
        ClosedPins.Capacity = ClosedPinLimits.Capacity(closed.Settings.MaxRecoverableClosedPins);
        ClosedPins.Add(closed, kind);
    }

    public static void Add(PinWindow pin)
    {
        Pins.Add(pin);
        pin.Closed += (_, _) => Remove(pin);
        if (_hook == IntPtr.Zero) InstallHook();
    }

    private static void Remove(PinWindow pin)
    {
        Pins.Remove(pin);
        HiddenPins.Remove(pin);
        if (Pins.Count == 0) UninstallHook();
    }

    private static void InstallHook()
    {
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    private static void UninstallHook()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _proc = null;
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk == NativeMethods.VK_ESCAPE)
                OnEscape();
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void OnEscape()
    {
        // Don't interfere with cancelling a capture, or a pin's own dialog.
        if (CaptureActive || _dialogDepth > 0) return;

        IntPtr fg = NativeMethods.GetForegroundWindow();

        // If a pin is focused, its own Esc handler closes just that one — do nothing here.
        bool pinFocused = Pins.Any(p => new WindowInteropHelper(p).Handle == fg);
        if (pinFocused) return;

        // No pin focused → close every shown one, recoverably (marshal onto the UI thread to
        // be safe). Hidden pins are put away, not in the way, so they stay.
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var pin in Pins.Where(p => p.IsVisible && p.Settings.PinGlobalEscClosesAll).ToList())
                CloseRecoverable(pin);
        }));
    }
}
