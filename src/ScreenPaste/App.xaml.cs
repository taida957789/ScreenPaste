using System.Diagnostics;
using System.IO;
using System.Windows;
using ScreenPaste.Capture;
using ScreenPaste.Native;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenPaste;

public partial class App : Application
{
    private const string MutexName = "ScreenPaste_SingleInstance_Mutex";

    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private HotkeyManager? _hotkey;
    private AppSettings _settings = new();
    private CaptureOverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Must run before ANY window (incl. the hidden hotkey sink) is created.
        try { NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* older OS: falls back to manifest/default */ }

        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            Forms.MessageBox.Show("ScreenPaste 已經在執行中（請查看系統匣）。", "ScreenPaste",
                Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        Theme.Apply(_settings.Theme);
        StartupManager.Sync(_settings.RunAtStartup);

        SetupTray();
        SetupHotkey();
    }

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => StartCapture();
        RefreshTray();
    }

    /// <summary>(Re)build the tray menu — reflects the current hotkey label and theme.</summary>
    private void RefreshTray()
    {
        if (_tray == null) return;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截圖 (" + _settings.CaptureHotkey + ")", null, (_, _) => StartCapture());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("設定…", null, (_, _) => OpenSettings());
        menu.Items.Add("開啟儲存資料夾", null, (_, _) => OpenSaveFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("結束", null, (_, _) => ExitApp());
        ApplyMenuTheme(menu);
        _tray.ContextMenuStrip = menu;
        _tray.Text = "ScreenPaste — 按 " + _settings.CaptureHotkey + " 截圖";
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true }) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings, ApplySettingsChanged);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Apply GUI settings changes: theme, startup, capture hotkey, tray.</summary>
    private void ApplySettingsChanged()
    {
        Theme.Apply(_settings.Theme);
        StartupManager.Sync(_settings.RunAtStartup);
        var win32 = HotkeyGesture.ToWin32(_settings.CaptureHotkey);
        if (win32 is { } hk) _hotkey?.Register(hk.modifiers, hk.virtualKey);
        RefreshTray();
    }

    /// <summary>Tint the tray menu to match the current theme.</summary>
    private static void ApplyMenuTheme(Forms.ContextMenuStrip menu)
    {
        var back = Theme.IsDark ? Drawing.Color.FromArgb(0x2A, 0x2A, 0x2A) : Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        var fore = Theme.IsDark ? Drawing.Color.White : Drawing.Color.FromArgb(0x20, 0x20, 0x20);
        ApplyMenuColors(menu.Items, back, fore);
        menu.BackColor = back;
        menu.ForeColor = fore;
    }

    private static void ApplyMenuColors(Forms.ToolStripItemCollection items, Drawing.Color back, Drawing.Color fore)
    {
        foreach (Forms.ToolStripItem item in items)
        {
            item.BackColor = back;
            item.ForeColor = fore;
            if (item is Forms.ToolStripMenuItem mi && mi.HasDropDownItems)
            {
                mi.DropDown.BackColor = back;
                ApplyMenuColors(mi.DropDownItems, back, fore);
            }
        }
    }

    private void SetupHotkey()
    {
        _hotkey = new HotkeyManager();
        _hotkey.Pressed += StartCapture;

        var win32 = HotkeyGesture.ToWin32(_settings.CaptureHotkey);
        bool ok = win32 is { } hk && _hotkey.Register(hk.modifiers, hk.virtualKey);
        if (!ok)
        {
            _tray!.ShowBalloonTip(3000, "ScreenPaste",
                $"截圖熱鍵「{_settings.CaptureHotkey}」註冊失敗（無效或被占用）。仍可由系統匣截圖。",
                Forms.ToolTipIcon.Warning);
        }
    }

    private void StartCapture()
    {
        // Ignore if a capture is already in progress.
        if (_overlay is { IsVisible: true }) return;

        try
        {
            Theme.Apply(_settings.Theme);   // pick up OS light/dark changes when following system
            var screenshot = ScreenCapture.CaptureVirtualScreen(out var vs);
            _overlay = new CaptureOverlayWindow(screenshot, vs, _settings);
            PinManager.CaptureActive = true;
            _overlay.Closed += (_, _) => { _overlay = null; PinManager.CaptureActive = false; };
            _overlay.Show();
        }
        catch (Exception ex)
        {
            _overlay = null;
            _tray?.ShowBalloonTip(3000, "ScreenPaste", "截圖失敗：" + ex.Message, Forms.ToolTipIcon.Error);
        }
    }

    private void OpenSaveFolder()
    {
        try
        {
            Directory.CreateDirectory(_settings.SaveDirectory);
            Process.Start(new ProcessStartInfo(_settings.SaveDirectory) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private void ExitApp()
    {
        _overlay?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
