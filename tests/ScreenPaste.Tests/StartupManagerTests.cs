using Microsoft.Win32;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// "Run at startup" as the per-user Run key holds it. The installer's "run at startup" task
/// writes that value too, so at launch the app takes it as its setting instead of putting its
/// setting over it. Each test works under a scratch key of its own in place of HKCU, removed after.
/// </summary>
public sealed class StartupManagerTests : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ThisCopy = @"C:\Apps\ScreenPaste\ScreenPaste.exe";
    private const string OtherCopy = @"C:\Users\me\AppData\Roaming\ScreenPaste\ScreenPaste.exe";

    private readonly string _rootPath = $@"Software\ScreenPaste.Tests-{Guid.NewGuid():N}";
    private readonly RegistryKey _root;

    public StartupManagerTests() => _root = Registry.CurrentUser.CreateSubKey(_rootPath);

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_rootPath, throwOnMissingSubKey: false);
    }

    private void Register(string command)
    {
        using var key = _root.CreateSubKey(RunKey);
        key.SetValue("ScreenPaste", command);
    }

    private string? Registered()
    {
        using var key = _root.OpenSubKey(RunKey);
        return key?.GetValue("ScreenPaste") as string;
    }

    private static bool Everything(string path) => true;

    [Fact]
    public void Launch_IsOff_WhenNothingIsRegistered()
    {
        Assert.False(StartupManager.Reconcile(_root, ThisCopy, Everything));
        Assert.Null(Registered());
    }

    [Fact]
    public void Launch_IsOn_WhenTheInstallerRegisteredIt_AndKeepsIt()
    {
        Register($"\"{ThisCopy}\"");

        Assert.True(StartupManager.Reconcile(_root, ThisCopy, Everything));
        Assert.Equal($"\"{ThisCopy}\"", Registered());
    }

    [Theory]
    [InlineData(@"""C:\Old\ScreenPaste.exe""")]   // moved or deleted
    [InlineData("\"")]                             // not a program at all
    public void Launch_PointsAnEntryWhoseProgramIsGone_AtThisCopy(string command)
    {
        Register(command);

        Assert.True(StartupManager.Reconcile(_root, ThisCopy, path => path == ThisCopy));
        Assert.Equal($"\"{ThisCopy}\"", Registered());
    }

    [Fact]
    public void Launch_LeavesAnEntryForAnotherCopyThatIsStillThere()
    {
        // A development build started while the installed copy is the one that starts with Windows.
        Register($"\"{OtherCopy}\"");

        Assert.True(StartupManager.Reconcile(_root, ThisCopy, Everything));
        Assert.Equal($"\"{OtherCopy}\"", Registered());
    }

    [Fact]
    public void Launch_WritesNoEmptyPath_WhenItsOwnIsNotKnown()
    {
        Register(@"""C:\Old\ScreenPaste.exe""");

        Assert.True(StartupManager.Reconcile(_root, "", _ => false));
        Assert.Equal(@"""C:\Old\ScreenPaste.exe""", Registered());
    }

    [Fact]
    public void Sync_TurnsItOnAndOff_AsTheSettingSays()
    {
        StartupManager.Sync(_root, true, ThisCopy);
        Assert.Equal($"\"{ThisCopy}\"", Registered());
        Assert.True(StartupManager.IsEnabled(_root));

        StartupManager.Sync(_root, false, ThisCopy);
        Assert.Null(Registered());
        Assert.False(StartupManager.IsEnabled(_root));
    }

    [Fact]
    public void Sync_On_LeavesAnEntryThatIsAlreadyThere()
    {
        Register($"\"{OtherCopy}\"");

        StartupManager.Sync(_root, true, ThisCopy);

        Assert.Equal($"\"{OtherCopy}\"", Registered());
    }

    [Theory]
    [InlineData(@"""C:\Program Files\ScreenPaste\ScreenPaste.exe""", @"C:\Program Files\ScreenPaste\ScreenPaste.exe")]
    [InlineData(@"""C:\Program Files\ScreenPaste\ScreenPaste.exe"" --tray", @"C:\Program Files\ScreenPaste\ScreenPaste.exe")]
    [InlineData(@"C:\Apps\ScreenPaste.exe --tray", @"C:\Apps\ScreenPaste.exe")]
    [InlineData(@"C:\tools.exe.d\ScreenPaste.exe --tray", @"C:\tools.exe.d\ScreenPaste.exe")]
    [InlineData(@"  C:\Apps\ScreenPaste.EXE  ", @"C:\Apps\ScreenPaste.EXE")]
    [InlineData(@"C:\Apps\ScreenPaste", @"C:\Apps\ScreenPaste")]
    [InlineData("", null)]
    [InlineData("\"", null)]
    [InlineData("\"\"", null)]
    public void ExeOf_IsTheProgramARunEntryStarts(string command, string? expected)
    {
        Assert.Equal(expected, StartupManager.ExeOf(command));
    }

    [Fact]
    public void InstallerArguments_SetTheInstallersTask_AsTheSettingIs()
    {
        Assert.Equal("/MERGETASKS=\"startup\"", StartupManager.InstallerArguments(true));
        Assert.Equal("/MERGETASKS=\"!startup\"", StartupManager.InstallerArguments(false));
    }
}
