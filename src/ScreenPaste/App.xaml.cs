using System.Diagnostics;
using System.IO;
using System.Windows;
using ScreenPaste.Capture;
using ScreenPaste.Native;
using ScreenPaste.Output;
using ScreenPaste.Recording;
using ScreenPaste.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenPaste;

public partial class App : Application
{
    private const string MutexName = "ScreenPaste_SingleInstance_Mutex";
    private const int HkCapture = 0xB001;
    private const int HkRecord = 0xB002;

    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private HotkeyManager? _hotkey;
    private AppSettings _settings = new();
    private CaptureOverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private ScreenRecorder? _recorder;
    private RecordingHud? _hud;
    private bool _selecting;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Must run before ANY window (incl. the hidden hotkey sink) is created.
        try { NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* older OS: falls back to manifest/default */ }

        base.OnStartup(e);

        // Every window gets a theme-matched (dark/light) title bar; borderless windows
        // (overlay, HUD, pins) simply have no title bar for this to affect.
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (s is Window w) Theme.StyleTitleBar(w); }));

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

        _tray?.ShowBalloonTip(3000, "ScreenPaste",
            Loc.T("tray.started", _settings.CaptureHotkey), Forms.ToolTipIcon.Info);

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
        menu.Items.Add(
            _recorder != null
                ? Loc.T("tray.recordStop")
                : Loc.T("tray.record") + " (" + _settings.RecordHotkey + ")",
            null, (_, _) => ToggleRecord());
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

    /// <summary>Apply GUI settings changes: theme, startup, hotkeys, tray.</summary>
    private void ApplySettingsChanged()
    {
        Loc.Init(_settings.Language);
        Theme.Apply(_settings.Theme);
        StartupManager.Sync(_settings.RunAtStartup);
        RegisterHotkeys(warn: false);
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
        _hotkey.Pressed += OnHotkeyPressed;
        RegisterHotkeys(warn: true);
    }

    /// <summary>(Re)register the capture and record global hotkeys from settings.</summary>
    private void RegisterHotkeys(bool warn)
    {
        if (_hotkey == null) return;

        var cap = HotkeyGesture.ToWin32(_settings.CaptureHotkey);
        bool capOk = cap is { } c && _hotkey.Register(HkCapture, c.modifiers, c.virtualKey);
        if (!capOk && warn)
            _tray?.ShowBalloonTip(3000, "ScreenPaste",
                Loc.T("msg.hotkeyFail", _settings.CaptureHotkey), Forms.ToolTipIcon.Warning);

        var rec = HotkeyGesture.ToWin32(_settings.RecordHotkey);
        bool recWanted = !string.IsNullOrWhiteSpace(_settings.RecordHotkey);
        bool recOk = rec is { } r && _hotkey.Register(HkRecord, r.modifiers, r.virtualKey);
        if (!recOk) _hotkey.Unregister(HkRecord);
        if (recWanted && !recOk && warn)
            _tray?.ShowBalloonTip(3000, "ScreenPaste",
                Loc.T("msg.hotkeyFail", _settings.RecordHotkey), Forms.ToolTipIcon.Warning);
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == HkCapture) StartCapture();
        else if (id == HkRecord) ToggleRecord();
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

    /// <summary>Hotkey/menu entry point: start a region recording, or stop the running one.</summary>
    private void ToggleRecord()
    {
        if (_recorder != null) { StopRecording(); return; }
        if (_selecting) return;
        StartRecording();
    }

    private void StartRecording()
    {
        Int32Rect region;
        _selecting = true;
        try
        {
            Theme.Apply(_settings.Theme);
            var selector = new RegionSelectorWindow();
            if (selector.ShowDialog() != true || selector.Selection is not { } sel) return;
            region = sel;
        }
        finally { _selecting = false; }

        // Default flow records a near-lossless intermediate for the post-record editor;
        // the "skip editor" setting keeps the original record-straight-to-file behavior.
        RecordingFormat? format;
        string path;
        if (_settings.RecordSkipEditor)
        {
            var final = RecordingFormats.Parse(_settings.RecordFormat);
            format = final;
            Directory.CreateDirectory(_settings.SaveDirectory);
            path = Path.Combine(_settings.SaveDirectory,
                "ScreenPaste_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + final.Extension());
        }
        else
        {
            format = null;
            var tempDir = Path.Combine(Path.GetTempPath(), "ScreenPaste");
            Directory.CreateDirectory(tempDir);
            path = Path.Combine(tempDir, "rec_" + Guid.NewGuid().ToString("N") + ".mp4");
        }

        try
        {
            var recorder = new ScreenRecorder(region, _settings.RecordFps, format, path,
                _settings.RecordCaptureCursor);
            recorder.Start();
            _recorder = recorder;

            _hud = new RecordingHud(region);
            _hud.StopRequested += StopRecording;
            _hud.Show();
            RefreshTray();
        }
        catch (FFmpegNotFoundException)
        {
            _tray?.ShowBalloonTip(6000, "ScreenPaste", Loc.T("rec.noFfmpeg"), Forms.ToolTipIcon.Error);
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloonTip(4000, "ScreenPaste", Loc.T("rec.failed", ex.Message), Forms.ToolTipIcon.Error);
        }
    }

    private async void StopRecording()
    {
        var recorder = _recorder;
        if (recorder == null) return;
        _recorder = null;

        _hud?.Close();
        _hud = null;
        RefreshTray();

        // Direct-save recordings encode the final GIF while flushing, which can take a
        // moment; intermediate flushes are near-instant, so no balloon there.
        if (recorder.Format != null)
            _tray?.ShowBalloonTip(2000, "ScreenPaste", Loc.T("rec.encoding"), Forms.ToolTipIcon.Info);

        bool ok = await recorder.StopAsync();
        if (!ok)
        {
            _tray?.ShowBalloonTip(5000, "ScreenPaste",
                Loc.T("rec.failed", "ffmpeg"), Forms.ToolTipIcon.Error);
            if (recorder.Format == null)
                try { File.Delete(recorder.OutputPath); } catch { /* temp cleanup */ }
            return;
        }

        if (recorder.Format != null)
        {
            _tray?.ShowBalloonTip(4000, "ScreenPaste",
                Loc.T("rec.saved", Path.GetFileName(recorder.OutputPath)), Forms.ToolTipIcon.Info);
            return;
        }

        // Editor flow: open the trim/export editor on the intermediate clip.
        var editor = new RecordingEditorWindow(
            recorder.OutputPath, recorder.FrameWidth, recorder.FrameHeight, recorder.Fps,
            _settings,
            saved => _tray?.ShowBalloonTip(4000, "ScreenPaste",
                Loc.T("rec.saved", Path.GetFileName(saved)), Forms.ToolTipIcon.Info));
        editor.Show();
        editor.Activate();
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
        _hud?.Close();
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
