using System.Windows;
using ScreenPaste.Capture;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// R / Shift+R: the areas of the last successful snips, newest first, kept for the app's
/// lifetime. R steps back through them and Shift+R the other way, both wrapping around; a
/// new capture starts again from the newest.
/// </summary>
public sealed class SnipRegionHistoryTests
{
    private static readonly Rect A = new(10, 10, 100, 80);
    private static readonly Rect B = new(200, 50, 300, 200);
    private static readonly Rect C = new(-1500, 40, 640, 480);   // on a monitor left of the primary

    private static SnipRegionHistory With(params Rect[] oldestFirst)
    {
        var history = new SnipRegionHistory();
        foreach (var r in oldestFirst) history.Push(r);
        return history;
    }

    // ---------------------------------------------------------------- push ---

    [Fact]
    public void Push_PutsTheNewestFirst()
    {
        var history = With(A, B, C);

        Assert.Equal(new[] { C, B, A }, history.Regions);
        Assert.Equal(C, history.Latest);
    }

    [Fact]
    public void Push_CollapsesARepeatOfTheNewest()
    {
        var history = With(A, B, B);

        Assert.Equal(new[] { B, A }, history.Regions);
    }

    [Fact]
    public void Push_KeepsAnOlderDuplicate_OnlyConsecutiveOnesCollapse()
    {
        var history = With(A, B, A);

        Assert.Equal(new[] { A, B, A }, history.Regions);
    }

    [Fact]
    public void Push_KeepsAtMostEight_DroppingTheOldest()
    {
        var history = new SnipRegionHistory();
        for (int i = 0; i < 10; i++) history.Push(new Rect(i, 0, 50, 50));

        Assert.Equal(SnipRegionHistory.DefaultCapacity, history.Count);
        Assert.Equal(8, history.Count);
        Assert.Equal(new Rect(9, 0, 50, 50), history.Regions[0]);
        Assert.Equal(new Rect(2, 0, 50, 50), history.Regions[^1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 40)]
    [InlineData(40, 0)]
    public void Push_IgnoresAnAreaWithNoSize(double width, double height)
    {
        var history = With(A);

        history.Push(new Rect(5, 5, width, height));

        Assert.Equal(new[] { A }, history.Regions);
    }

    [Fact]
    public void Push_IgnoresAnEmptyRect()
    {
        var history = With(A);

        history.Push(Rect.Empty);

        Assert.Equal(new[] { A }, history.Regions);
    }

    // ---------------------------------------------------------------- R / Shift+R ---

    [Fact]
    public void Previous_StartsAtTheLastSnip_ThenStepsBackThroughOlderOnes()
    {
        var history = With(A, B, C);

        Assert.Equal(C, history.Previous());
        Assert.Equal(B, history.Previous());
        Assert.Equal(A, history.Previous());
    }

    [Fact]
    public void Previous_WrapsFromTheOldestBackToTheNewest()
    {
        var history = With(A, B);
        history.Previous();
        history.Previous();

        Assert.Equal(B, history.Previous());
    }

    [Fact]
    public void Next_AtTheStart_GoesTheOtherWay_ToTheOldest()
    {
        var history = With(A, B, C);

        Assert.Equal(A, history.Next());
        Assert.Equal(B, history.Next());
        Assert.Equal(C, history.Next());
    }

    [Fact]
    public void Next_AfterPrevious_StepsBackTowardsTheNewest()
    {
        var history = With(A, B, C);
        history.Previous();   // C
        history.Previous();   // B

        Assert.Equal(C, history.Next());
    }

    [Fact]
    public void Next_WrapsFromTheNewestToTheOldest()
    {
        var history = With(A, B, C);
        history.Previous();   // C, the newest

        Assert.Equal(A, history.Next());
    }

    [Fact]
    public void PreviousAndNext_WithNoSnipsYet_GiveNothing()
    {
        var history = new SnipRegionHistory();

        Assert.Null(history.Previous());
        Assert.Null(history.Next());
        Assert.Null(history.Latest);
    }

    [Fact]
    public void Reset_StartsOverAtTheNewest()
    {
        var history = With(A, B, C);
        history.Previous();
        history.Previous();

        history.Reset();

        Assert.Equal(C, history.Previous());
    }

    [Fact]
    public void Push_StartsOverAtTheNewest()
    {
        var history = With(A, B);
        history.Previous();
        history.Previous();

        history.Push(C);

        Assert.Equal(C, history.Previous());
    }

    [Fact]
    public void Clear_ForgetsEveryArea()
    {
        var history = With(A, B);

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Null(history.Previous());
    }

    // ---------------------------------------------------------------- fitting ---

    [Fact]
    public void FitInto_MapsScreenPixelsToTheOverlay()
    {
        var screen = new VirtualScreen(-1920, 0, 3840, 1080);

        var local = SnipRegionHistory.FitInto(C, screen);

        Assert.Equal(new Rect(420, 40, 640, 480), local);
    }

    [Fact]
    public void FitInto_MovesAnAreaFromAMonitorThatIsGone_BackOnScreen_KeepingItsSize()
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);
        var onAMissingMonitor = new Rect(2500, 900, 400, 300);

        var local = SnipRegionHistory.FitInto(onAMissingMonitor, screen);

        Assert.Equal(new Rect(1520, 780, 400, 300), local);
    }

    [Fact]
    public void FitInto_ShrinksAnAreaLargerThanTheSmallerScreen()
    {
        var screen = new VirtualScreen(0, 0, 1280, 720);
        var bothMonitors = new Rect(0, 0, 3840, 1080);

        var local = SnipRegionHistory.FitInto(bothMonitors, screen);

        Assert.Equal(new Rect(0, 0, 1280, 720), local);
    }

    [Fact]
    public void FitInto_LeavesAnAreaThatFitsAlone()
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        Assert.Equal(B, SnipRegionHistory.FitInto(B, screen));
    }
}
