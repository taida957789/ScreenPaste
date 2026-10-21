using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Copy as file (Ctrl+Alt+C) writes the capture to %TEMP%\ScreenPaste so it can be pasted
/// into Explorer or an upload box; app startup clears out the copies older than a day. Only
/// the naming and the age rule are tested here: the clipboard itself needs a desktop.
/// </summary>
public sealed class ClipboardServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ClipFileName_IsATimestampedPng_DownToTheMillisecond()
    {
        var at = new DateTime(2026, 10, 20, 9, 5, 7, 42);

        Assert.Equal("clip_20261020_090507_042.png", ClipboardService.ClipFileName(at));
    }

    [Fact]
    public void ClipFileName_DiffersForTwoCopiesInTheSameSecond()
    {
        var first = new DateTime(2026, 10, 20, 9, 5, 7, 100);

        Assert.NotEqual(ClipboardService.ClipFileName(first), ClipboardService.ClipFileName(first.AddMilliseconds(1)));
    }

    [Fact]
    public void IsStaleClip_OnlyForClipsOlderThanTheMaximumAge()
    {
        var name = ClipboardService.ClipFileName(Now);

        Assert.True(ClipboardService.IsStaleClip(name, Now.AddDays(-1).AddSeconds(-1), Now));
        Assert.False(ClipboardService.IsStaleClip(name, Now.AddHours(-23), Now));
    }

    [Theory]
    [InlineData("rec_0123456789abcdef.mp4")]   // a recording's intermediate clip
    [InlineData("ov_0123456789abcdef.png")]    // a recording export's overlay
    [InlineData("clip_notes.txt")]
    [InlineData("ScreenPaste-update-setup.exe")]
    public void IsStaleClip_LeavesOtherFilesInTheFolderAlone(string name)
    {
        Assert.False(ClipboardService.IsStaleClip(name, Now.AddDays(-30), Now));
    }

    [Fact]
    public void IsStaleClip_IgnoresTheCaseOfTheName()
    {
        Assert.True(ClipboardService.IsStaleClip("CLIP_20250101_000000_000.PNG", Now.AddDays(-30), Now));
    }
}
