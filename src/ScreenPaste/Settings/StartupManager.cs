using Microsoft.Win32;

namespace ScreenPaste.Settings;

/// <summary>Toggles "run at Windows startup" via the per-user Run registry key.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenPaste";

    private static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;

            if (enabled)
                key.SetValue(ValueName, $"\"{ExePath}\"");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* non-fatal */ }
    }

    /// <summary>Reconcile the registry entry with the saved preference at startup.</summary>
    public static void Sync(bool preferred)
    {
        if (preferred != IsEnabled()) SetEnabled(preferred);
    }
}
