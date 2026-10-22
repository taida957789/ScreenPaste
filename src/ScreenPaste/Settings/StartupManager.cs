using System.IO;
using Microsoft.Win32;

namespace ScreenPaste.Settings;

/// <summary>
/// "Run at Windows startup" via the per-user Run registry key. That value is what counts: the
/// installer's "run at startup" task writes it too, so at launch the app reads it back as its
/// setting (<see cref="Reconcile()"/>) and writes it only when the setting is changed
/// (<see cref="Sync(bool)"/>).
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenPaste";

    private static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsEnabled() => IsEnabled(Registry.CurrentUser);

    /// <summary>Put the setting, as just changed in Settings, into the registry.</summary>
    public static void Sync(bool preferred) => Sync(Registry.CurrentUser, preferred, ExePath);

    /// <summary>At launch: whether it is on, as the Run key says, so the installer's choice holds.
    /// An entry whose program is gone (a copy moved or deleted) is pointed at this one.</summary>
    public static bool Reconcile() => Reconcile(Registry.CurrentUser, ExePath, File.Exists);

    /// <summary>Arguments for the installer an update starts, so its "run at startup" task is
    /// set as the setting is now rather than as it was chosen at the first install.</summary>
    public static string InstallerArguments(bool enabled) =>
        enabled ? "/MERGETASKS=\"startup\"" : "/MERGETASKS=\"!startup\"";

    internal static bool IsEnabled(RegistryKey root) => Registered(root) != null;

    internal static void SetEnabled(RegistryKey root, bool enabled, string exePath)
    {
        try
        {
            using var key = root.OpenSubKey(RunKey, writable: true) ?? root.CreateSubKey(RunKey);
            if (key == null) return;

            if (enabled)
                key.SetValue(ValueName, $"\"{exePath}\"");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* non-fatal */ }
    }

    internal static void Sync(RegistryKey root, bool preferred, string exePath)
    {
        if (preferred != IsEnabled(root)) SetEnabled(root, preferred, exePath);
    }

    internal static bool Reconcile(RegistryKey root, string exePath, Func<string, bool> exists)
    {
        if (Registered(root) is not { } command) return false;
        // Another copy that is still there keeps its entry (a development build leaves the
        // installed one be); only an entry that starts nothing is taken over.
        if (exePath.Length > 0 && (ExeOf(command) is not { } exe || !exists(exe)))
            SetEnabled(root, true, exePath);
        return true;
    }

    /// <summary>The program a Run entry starts: its quoted path, or unquoted up to the ".exe"
    /// its arguments follow (not one inside a folder's name); null when there is none.</summary>
    internal static string? ExeOf(string command)
    {
        var text = command.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }
        for (var at = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(".exe", at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + 4;
            if (end == text.Length || text[end] == ' ') return text[..end];
        }
        return text.Length > 0 ? text : null;
    }

    private static string? Registered(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) as string;
        }
        catch { return null; }
    }
}
