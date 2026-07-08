using System.Windows.Interop;
using static ScreenPaste.Native.NativeMethods;

namespace ScreenPaste.Native;

/// <summary>
/// Registers system-wide hotkeys against a hidden message-only WPF window and
/// raises <see cref="Pressed"/> (with the hotkey id) when one fires. Multiple
/// hotkeys can share the single sink, each identified by its id. Dispose to unregister all.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly HashSet<int> _registered = new();

    /// <summary>Raised on the UI thread with the id of the hotkey that fired.</summary>
    public event Action<int>? Pressed;

    public HotkeyManager()
    {
        // Message-only window: never shown, just a sink for WM_HOTKEY.
        var parameters = new HwndSourceParameters("ScreenPasteHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IntPtr Handle => _source.Handle;

    /// <summary>Register (or re-register) a hotkey by id. Returns false if the combo is taken.</summary>
    public bool Register(int id, uint modifiers, uint virtualKey)
    {
        Unregister(id);
        bool ok = RegisterHotKey(_source.Handle, id, modifiers | MOD_NOREPEAT, virtualKey);
        if (ok) _registered.Add(id);
        return ok;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
            UnregisterHotKey(_source.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_registered.Contains(id))
            {
                handled = true;
                Pressed?.Invoke(id);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered) UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
