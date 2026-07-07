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
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截圖 (" + _settings.CaptureHotkey + ")", null, (_, _) => StartCapture());
        menu.Items.Add(new Forms.ToolStripSeparator());

        // Appearance submenu: System / Light / Dark
        var themeMenu = new Forms.ToolStripMenuItem("外觀主題");
        AddThemeItem(themeMenu, "跟隨系統", "System");
        AddThemeItem(themeMenu, "淺色", "Light");
        AddThemeItem(themeMenu, "深色", "Dark");
        menu.Items.Add(themeMenu);

        var startup = new Forms.ToolStripMenuItem("開機時自動啟動")
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled(),
        };
        startup.Click += (_, _) =>
        {
            _settings.RunAtStartup = startup.Checked;
            StartupManager.SetEnabled(startup.Checked);
            _settings.Save();
        };
        menu.Items.Add(startup);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("開啟儲存資料夾", null, (_, _) => OpenSaveFolder());
        menu.Items.Add("設定檔…", null, (_, _) => OpenSettingsFile());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("結束", null, (_, _) => ExitApp());

        ApplyMenuTheme(menu);

        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Visible = true,
            Text = "ScreenPaste — 按 " + _settings.CaptureHotkey + " 截圖",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => StartCapture();
    }

    private void AddThemeItem(Forms.ToolStripMenuItem parent, string label, string value)
    {
        var item = new Forms.ToolStripMenuItem(label)
        {
            Checked = string.Equals(_settings.Theme, value, StringComparison.OrdinalIgnoreCase),
        };
        item.Click += (_, _) =>
        {
            _settings.Theme = value;
            _settings.Save();
            Theme.Apply(value);
            foreach (Forms.ToolStripMenuItem sib in parent.DropDownItems)
                sib.Checked = sib == item;
            if (_tray?.ContextMenuStrip != null) ApplyMenuTheme(_tray.ContextMenuStrip);
        };
        parent.DropDownItems.Add(item);
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

    private void OpenSettingsFile()
    {
        try
        {
            _settings.Save(); // ensure it exists
            Process.Start(new ProcessStartInfo(AppSettings.ConfigPath) { UseShellExecute = true });
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
