using System.Runtime.InteropServices;

namespace ScreenPaste.Native;

/// <summary>
/// Win32 P/Invoke declarations. The whole app runs Per-Monitor-V2 DPI aware,
/// so every coordinate exchanged with these APIs is in PHYSICAL pixels.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // ---- Virtual screen metrics (physical px in a DPI-aware process) ----
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // ---- Window enumeration & geometry ----
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const long WS_VISIBLE = 0x10000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_NOACTIVATE = 0x08000000L;

    // ---- DWM: true visible bounds (excludes drop shadow) & cloak state ----
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // ---- DPI ----
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 == (HANDLE)-4
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    // ---- Window placement (used to pin the overlay in physical px) ----
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    // ---- Global hotkey ----
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---- Cursor position (W/A/S/D nudges the pointer while snipping) ----
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int X, int Y);

    // ---- Keyboard layout (a pin's + and - go by the character a key types) ----
    public const uint MAPVK_VK_TO_CHAR = 2;

    /// <summary>With MAPVK_VK_TO_CHAR: the character the key types unshifted on the current
    /// layout in the low word, 0 for none; the top bit is set for a dead key.</summary>
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // ---- Foreground window ----
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // ---- Low-level keyboard hook (for global Esc while pins exist) ----
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int VK_ESCAPE = 0x1B;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ---- Extended window styles (click-through recording border) ----
    public const long WS_EX_LAYERED = 0x00080000L;
    public const long WS_EX_TRANSPARENT = 0x00000020L;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Turn on <paramref name="styles"/> (WS_EX_*) for the window, keeping the rest.</summary>
    public static void AddExStyle(IntPtr hWnd, long styles) =>
        SetWindowLong(hWnd, GWL_EXSTYLE, GetWindowLong(hWnd, GWL_EXSTYLE) | (int)styles);

    /// <summary>Turn off <paramref name="styles"/> (WS_EX_*) for the window, keeping the rest.</summary>
    public static void RemoveExStyle(IntPtr hWnd, long styles) =>
        SetWindowLong(hWnd, GWL_EXSTYLE, GetWindowLong(hWnd, GWL_EXSTYLE) & ~(int)styles);

    // ---- Clipboard (the paste hotkey tells a new clipboard from the one it pasted last) ----
    /// <summary>Changes whenever the clipboard does; 0 without access to the clipboard.</summary>
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    // ---- Monitor DPI (a text pin is drawn at the scale of the monitor it opens on) ----
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    // ---- Cursor capture (drawing the pointer into recorded frames) ----
    public const int CURSOR_SHOWING = 0x0001;
    public const int DI_NORMAL = 0x0003;

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
        int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    // ---- Cursor snapshot (the pointer's image size, for a still screenshot) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    public static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    // ---- Display affinity (F5 re-captures the screen from under the overlay) ----
    // WDA_EXCLUDEFROMCAPTURE needs Windows 10 2004; older systems fail the call.
    public const uint WDA_NONE = 0x00000000;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>Blocks until the desktop compositor has presented its next frame.</summary>
    [DllImport("dwmapi.dll")]
    public static extern int DwmFlush();
}
