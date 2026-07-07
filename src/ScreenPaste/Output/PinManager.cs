using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenPaste.Native;

namespace ScreenPaste.Output;

/// <summary>
/// Tracks all open pins and provides the global-Esc policy:
///   • a focused pin closes itself (handled by the pin's own key handler);
///   • pressing Esc when NO pin is focused closes every pin at once.
/// The second case needs a low-level keyboard hook, installed only while pins exist.
/// </summary>
public static class PinManager
{
    private static readonly List<PinWindow> Pins = new();
    private static IntPtr _hook = IntPtr.Zero;
    private static NativeMethods.HookProc? _proc;   // kept alive to avoid GC of the callback

    /// <summary>Set while the capture overlay is up, so Esc there won't wipe existing pins.</summary>
    public static bool CaptureActive { get; set; }

    public static void Add(PinWindow pin)
    {
        Pins.Add(pin);
        pin.Closed += (_, _) => Remove(pin);
        if (_hook == IntPtr.Zero) InstallHook();
    }

    private static void Remove(PinWindow pin)
    {
        Pins.Remove(pin);
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
        // Don't interfere with cancelling a capture.
        if (CaptureActive) return;

        IntPtr fg = NativeMethods.GetForegroundWindow();

        // If a pin is focused, its own Esc handler closes just that one — do nothing here.
        bool pinFocused = Pins.Any(p => new WindowInteropHelper(p).Handle == fg);
        if (pinFocused) return;

        // No pin focused → close them all (marshal onto the UI thread to be safe).
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var pin in Pins.ToList())
                pin.Close();
        }));
    }
}
