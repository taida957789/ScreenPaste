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
    private AboutWindow? _aboutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Must run before ANY window (incl. the hidden hotkey sink) is created.
        try { NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* older OS: falls back to manifest/default */ }

        base.OnStartup(e);

        Loc.Init(null);   // system language until settings load

        _mutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            Forms.MessageBox.Show(Loc.T("msg.running"), "ScreenPaste",
                Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        Loc.Init(_settings.Language);
        Theme.Apply(_settings.Theme);
        StartupManager.Sync(_settings.RunAtStartup);

        SetupTray();
        SetupHotkey();

        if (_settings.CheckUpdateOnStartup) CheckForUpdates(silentIfNone: true);
    }

    private UpdateWindow? _updateWindow;

    /// <summary>Check GitHub for a newer release; prompt if found.</summary>
    private async void CheckForUpdates(bool silentIfNone)
    {
        var info = await UpdateChecker.CheckAsync();
        if (info != null)
        {
            if (_updateWindow is { IsVisible: true }) { _updateWindow.Activate(); return; }
            _updateWindow = new UpdateWindow(info);
            _updateWindow.Closed += (_, _) => _updateWindow = null;
            _updateWindow.Show();
            _updateWindow.Activate();
        }
        else if (!silentIfNone)
        {
            Forms.MessageBox.Show(Loc.T("upd.upToDate", UpdateChecker.Current.ToString()), "ScreenPaste",
                Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
        }
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
        menu.Items.Add(Loc.T("tray.capture") + " (" + _settings.CaptureHotkey + ")", null, (_, _) => StartCapture());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("tray.settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T("tray.openFolder"), null, (_, _) => OpenSaveFolder());
        menu.Items.Add(Loc.T("tray.about"), null, (_, _) => OpenAbout());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("tray.exit"), null, (_, _) => ExitApp());
        ApplyMenuTheme(menu);
        _tray.ContextMenuStrip = menu;
        _tray.Text = Loc.T("tray.tip", _settings.CaptureHotkey);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true }) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings, ApplySettingsChanged, () => CheckForUpdates(silentIfNone: false));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenAbout()
    {
        if (_aboutWindow is { IsVisible: true }) { _aboutWindow.Activate(); return; }
        _aboutWindow = new AboutWindow();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    /// <summary>Apply GUI settings changes: theme, startup, capture hotkey, tray.</summary>
    private void ApplySettingsChanged()
    {
        Loc.Init(_settings.Language);
        Theme.Apply(_settings.Theme);
        StartupManager.Sync(_settings.RunAtStartup);
        var win32 = HotkeyGesture.ToWin32(_settings.CaptureHotkey);
        if (win32 is { } hk) _hotkey?.Register(hk.modifiers, hk.virtualKey);
        RefreshTray();
    }

    /// <summary>Tint the tray menu to match the current theme (colours + renderer).</summary>
    private static void ApplyMenuTheme(Forms.ContextMenuStrip menu)
    {
        var back = Theme.IsDark ? Drawing.Color.FromArgb(0x2A, 0x2A, 0x2A) : Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        var fore = Theme.IsDark ? Drawing.Color.White : Drawing.Color.FromArgb(0x20, 0x20, 0x20);
        ApplyMenuColors(menu.Items, back, fore);
        menu.BackColor = back;
        menu.ForeColor = fore;
        // A renderer is required to theme the borders / image-margin gutter / separators.
        menu.RenderMode = Forms.ToolStripRenderMode.Professional;
        menu.Renderer = new Forms.ToolStripProfessionalRenderer(
            Theme.IsDark ? new DarkColorTable() : new Forms.ProfessionalColorTable());
    }

    /// <summary>Dark palette for the tray menu (borders, gutter, hover, separators).</summary>
    private sealed class DarkColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Bg = Drawing.Color.FromArgb(0x2A, 0x2A, 0x2A);
        private static readonly Drawing.Color Sel = Drawing.Color.FromArgb(0x3D, 0x3D, 0x3D);
        private static readonly Drawing.Color Bord = Drawing.Color.FromArgb(0x55, 0x55, 0x55);

        public override Drawing.Color ToolStripDropDownBackground => Bg;
        public override Drawing.Color ImageMarginGradientBegin => Bg;
        public override Drawing.Color ImageMarginGradientMiddle => Bg;
        public override Drawing.Color ImageMarginGradientEnd => Bg;
        public override Drawing.Color MenuBorder => Bord;
        public override Drawing.Color MenuItemBorder => Sel;
        public override Drawing.Color MenuItemSelected => Sel;
        public override Drawing.Color MenuItemSelectedGradientBegin => Sel;
        public override Drawing.Color MenuItemSelectedGradientEnd => Sel;
        public override Drawing.Color MenuItemPressedGradientBegin => Bg;
        public override Drawing.Color MenuItemPressedGradientEnd => Bg;
        public override Drawing.Color SeparatorDark => Bord;
        public override Drawing.Color SeparatorLight => Bord;
        public override Drawing.Color ToolStripBorder => Bg;
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
                Loc.T("msg.hotkeyFail", _settings.CaptureHotkey),
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
            _tray?.ShowBalloonTip(3000, "ScreenPaste", Loc.T("msg.captureFail", ex.Message), Forms.ToolTipIcon.Error);
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
