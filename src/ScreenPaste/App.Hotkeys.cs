using System.Windows;
using System.Windows.Threading;
using ScreenPaste.Capture;
using ScreenPaste.Native;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Forms = System.Windows.Forms;

namespace ScreenPaste;

/// <summary>
/// The global hotkeys (<see cref="GlobalHotkeyTable"/>): registered one by one, so one that
/// another app holds leaves the rest working and gets a balloon of its own; and what each
/// does: the snip modes, the custom and delayed snips, clearing the snip history. Pasting and
/// the pin hotkeys are App.Pins.cs's.
/// </summary>
public partial class App
{
    // A delayed start waits at least this long, so the custom snip dialog is off the screen.
    private static readonly TimeSpan MinSnipDelay = TimeSpan.FromMilliseconds(250);

    private Dictionary<int, string> _attemptedGestures = new();   // id -> gesture last registered (or tried)
    private bool _hotkeysHeld;                                    // the settings window is reading keys
    private DispatcherTimer? _pendingSnip;
    private CustomSnipDialog? _customSnip;

    private void SetupHotkey()
    {
        _hotkey = new HotkeyManager();
        _hotkey.Pressed += OnHotkeyPressed;
        RegisterHotkeys(warnAll: true);
    }

    /// <summary>
    /// (Re)register every assigned global hotkey from the settings. All are let go first, so
    /// two hotkeys can swap gestures. A failure is reported once: at startup (<paramref name="warnAll"/>),
    /// or later when its gesture was just changed.
    /// </summary>
    private void RegisterHotkeys(bool warnAll)
    {
        if (_hotkey == null || _hotkeysHeld) return;
        foreach (var hotkey in GlobalHotkeyTable.All) _hotkey.Unregister(hotkey.Id);

        var attempted = new Dictionary<int, string>();
        var failed = new List<GlobalHotkeyBinding>();
        foreach (var binding in GlobalHotkeyTable.Bindings(_settings))
        {
            int id = binding.Hotkey.Id;
            bool ok = binding.Win32 is { } w && _hotkey.Register(id, w.Modifiers, w.VirtualKey);
            bool changed = !_attemptedGestures.TryGetValue(id, out var before) || before != binding.Gesture;
            attempted[id] = binding.Gesture;
            if (!ok && (warnAll || changed)) failed.Add(binding);
        }
        _attemptedGestures = attempted;
        WarnHotkeysFailed(failed);
    }

    /// <summary>The tray's check item for the capture hotkey: unchecking lets F1 go (a game or
    /// another app wants it) without forgetting the gesture; checking takes it back.</summary>
    private Forms.ToolStripMenuItem CaptureHotkeySwitch()
    {
        bool assigned = !string.IsNullOrWhiteSpace(_settings.CaptureHotkey);
        var item = new Forms.ToolStripMenuItem(Loc.T("tray.captureHotkey", assigned ? _settings.CaptureHotkey.Trim() : "—"))
        {
            Checked = assigned && _settings.CaptureHotkeyEnabled,
            Enabled = assigned,
        };
        item.Click += (_, _) => ToggleCaptureHotkey();
        return item;
    }

    /// <summary>A "press F1 to capture" text, or, with the capture hotkey switched off or
    /// unassigned, its "double-click the icon" twin.</summary>
    private string CaptureHint(string withHotkey, string withoutHotkey) =>
        _settings.CaptureHotkeyEnabled && !string.IsNullOrWhiteSpace(_settings.CaptureHotkey)
            ? Loc.T(withHotkey, _settings.CaptureHotkey.Trim())
            : Loc.T(withoutHotkey);

    private void ToggleCaptureHotkey()
    {
        _settings.CaptureHotkeyEnabled = !_settings.CaptureHotkeyEnabled;
        _settings.Save();
        RegisterHotkeys(warnAll: false);   // switched back on but taken meanwhile: says so
        RefreshTray();
    }

    private void WarnHotkeysFailed(IReadOnlyList<GlobalHotkeyBinding> failed)
    {
        if (failed.Count == 0) return;
        // The capture hotkey alone keeps its own message: it says the tray still captures.
        var text = failed is [{ Hotkey.Action: GlobalHotkeyAction.Capture } only]
            ? Loc.T("msg.hotkeyFail", only.Gesture)
            : Loc.T("msg.hotkeysFail", string.Join(", ", failed.Select(f => $"{Loc.T(f.Hotkey.LabelKey)} ({f.Gesture})")));
        _tray?.ShowBalloonTip(4000, "ScreenPaste", text, Forms.ToolTipIcon.Warning);
    }

    /// <summary>While a hotkey field of the settings window has the keyboard, the global
    /// hotkeys are let go, so pressing F3 there types F3 rather than pasting.</summary>
    private void HoldHotkeys(bool hold)
    {
        if (_hotkey == null || hold == _hotkeysHeld) return;
        if (hold)
        {
            foreach (var hotkey in GlobalHotkeyTable.All) _hotkey.Unregister(hotkey.Id);
            _hotkeysHeld = true;
            return;
        }
        _hotkeysHeld = false;
        RegisterHotkeys(warnAll: false);
    }

    private void OnHotkeyPressed(int id)
    {
        switch (GlobalHotkeyTable.ById(id)?.Action)
        {
            case GlobalHotkeyAction.Capture: StartCapture(); break;
            case GlobalHotkeyAction.Record: ToggleRecord(); break;
            case GlobalHotkeyAction.SnipAndCopy: StartSnipMode(CaptureMode.Copy); break;
            case GlobalHotkeyAction.CustomSnip: OpenCustomSnip(); break;
            case GlobalHotkeyAction.Paste: PasteOrRestore(); break;
            case GlobalHotkeyAction.HidePins: PinManager.ToggleAllVisible(); break;
            case GlobalHotkeyAction.ClickThrough: ToggleClickThroughAtCursor(); break;
            case GlobalHotkeyAction.SnipAndPin: StartSnipMode(CaptureMode.Pin); break;
            case GlobalHotkeyAction.SnipCopyAndPin: StartSnipMode(CaptureMode.CopyAndPin); break;
            case GlobalHotkeyAction.LastAreaCopy: StartSnipMode(CaptureMode.LastAreaCopy); break;
            case GlobalHotkeyAction.FullScreenCopy: StartSnipMode(CaptureMode.FullScreenCopy); break;
            case GlobalHotkeyAction.ActiveWindow: StartSnipMode(CaptureMode.ActiveWindow); break;
            case GlobalHotkeyAction.ActiveScreen: StartSnipMode(CaptureMode.ActiveScreen); break;
            case GlobalHotkeyAction.DelayedSnip: StartAfterDelay(_settings.SnipDelaySeconds, StartCapture); break;
            case GlobalHotkeyAction.ClearSnipHistory: ClearSnipHistory(); break;
        }
    }

    // ---------------------------------------------------------- snip modes ---

    /// <summary>A capture in <paramref name="mode"/>, with the area the mode chooses up front
    /// (read now, before the overlay covers the screen). The last area with none yet, or an
    /// active window that cannot be read, leaves the area to frame by hand.</summary>
    private void StartSnipMode(CaptureMode mode)
    {
        Rect? preset = mode switch
        {
            CaptureMode.LastAreaCopy => _snipRegions.Latest,
            CaptureMode.FullScreenCopy => ScreenRect(VirtualScreen.Current),
            CaptureMode.ActiveWindow => ForegroundWindowRect(),
            CaptureMode.ActiveScreen => MonitorAtCursor(),
            _ => null,
        };
        StartCapture(mode, preset);
    }

    private static Rect ScreenRect(VirtualScreen vs) => new(vs.X, vs.Y, vs.Width, vs.Height);

    /// <summary>The foreground window's visible bounds (no drop shadow), or null.</summary>
    private static Rect? ForegroundWindowRect()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        var r = WindowEnumerator.GetVisibleBounds(hwnd);
        return r.Width > 0 && r.Height > 0 ? new Rect(r.Left, r.Top, r.Width, r.Height) : null;
    }

    /// <summary>The bounds of the monitor under the cursor (physical px).</summary>
    private static Rect MonitorAtCursor()
    {
        var b = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
        return new Rect(b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>Run <paramref name="start"/> after <paramref name="seconds"/> (0-60). A new
    /// delayed start replaces one still waiting.</summary>
    private void StartAfterDelay(double seconds, Action start)
    {
        _pendingSnip?.Stop();
        var delay = TimeSpan.FromSeconds(CustomSnipRequest.ClampDelay(seconds));
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay < MinSnipDelay ? MinSnipDelay : delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_pendingSnip == timer) _pendingSnip = null;
            start();
        };
        _pendingSnip = timer;
        timer.Start();
    }

    /// <summary>Shift+F1: the custom snip dialog, filled in with the last area (or the monitor
    /// under the cursor). Its area is chosen once the delay is over.</summary>
    private void OpenCustomSnip()
    {
        if (_overlay != null)
        {
            if (_overlay.IsVisible) _overlay.Activate();
            return;
        }
        if (_customSnip is { IsVisible: true }) { _customSnip.Activate(); return; }

        Theme.Apply(_settings.Theme);
        _customSnip = new CustomSnipDialog(_snipRegions.Latest ?? MonitorAtCursor(), _snipRegions.Latest, _settings.SnipDelaySeconds);
        _customSnip.Accepted += request =>
        {
            _settings.SnipDelaySeconds = request.DelaySeconds;
            _settings.Save();
            var r = request.Region;
            StartAfterDelay(request.DelaySeconds, () => StartCapture(CaptureMode.Normal, new Rect(r.X, r.Y, r.Width, r.Height)));
        };
        _customSnip.Closed += (_, _) => _customSnip = null;
        _customSnip.Show();
        _customSnip.Activate();
    }

    /// <summary>Forget the last areas (R / Shift+R) and the snip history (, / .).</summary>
    private void ClearSnipHistory()
    {
        _snipRegions.Clear();
        _snipRecords.Clear();
        _tray?.ShowBalloonTip(2000, "ScreenPaste", Loc.T("msg.historyCleared"), Forms.ToolTipIcon.Info);
    }
}
