using System.Windows.Input;
using ScreenPaste.Input;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// W/A/S/D (and the arrows while framing) move the OS cursor a pixel at a time, speeding up
/// while the key is held. Holding the left button at the same time turns that into pixel-
/// exact dragging, because the drag simply follows the cursor.
/// </summary>
public sealed class CursorNudgeTests
{
    // ---------------------------------------------------------------- Step ---

    [Theory]
    [InlineData(0, 1)]   // the press itself
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    public void Step_StartsAtOnePixel_ThenAccelerates(int repeatCount, int expected)
    {
        Assert.Equal(expected, CursorNudge.Step(repeatCount));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(40)]
    [InlineData(int.MaxValue)]
    public void Step_IsCappedAtTheMaximum(int repeatCount)
    {
        Assert.Equal(CursorNudge.MaxStep, CursorNudge.Step(repeatCount));
    }

    [Fact]
    public void Step_TreatsANegativeCountAsTheFirstPress()
    {
        Assert.Equal(1, CursorNudge.Step(-1));
    }

    // ---------------------------------------------------------------- Direction ---

    [Theory]
    [InlineData(Key.W, 0, -1)]
    [InlineData(Key.Up, 0, -1)]
    [InlineData(Key.A, -1, 0)]
    [InlineData(Key.Left, -1, 0)]
    [InlineData(Key.S, 0, 1)]
    [InlineData(Key.Down, 0, 1)]
    [InlineData(Key.D, 1, 0)]
    [InlineData(Key.Right, 1, 0)]
    public void Direction_MapsWasdAndTheArrowsToUnitSteps(Key key, int dx, int dy)
    {
        Assert.Equal((dx, dy), CursorNudge.Direction(key));
    }

    [Theory]
    [InlineData(Key.Q)]
    [InlineData(Key.Space)]
    public void Direction_IsZero_ForAnyOtherKey(Key key)
    {
        Assert.Equal((0, 0), CursorNudge.Direction(key));
        Assert.False(CursorNudge.IsWasd(key));
        Assert.False(CursorNudge.IsArrow(key));
    }

    [Fact]
    public void IsWasdAndIsArrow_SplitTheTwoKeyGroups()
    {
        Assert.True(CursorNudge.IsWasd(Key.W));
        Assert.False(CursorNudge.IsWasd(Key.Up));
        Assert.True(CursorNudge.IsArrow(Key.Left));
        Assert.False(CursorNudge.IsArrow(Key.A));
    }

    // ---------------------------------------------------------------- Clamp ---

    [Fact]
    public void Clamp_LeavesAnOnScreenPointAlone()
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        Assert.Equal((500, 400), CursorNudge.Clamp(500, 400, screen));
    }

    [Theory]
    [InlineData(-1, 500, 0, 500)]
    [InlineData(1920, 500, 1919, 500)]
    [InlineData(500, -8, 500, 0)]
    [InlineData(500, 1080, 500, 1079)]
    public void Clamp_StopsAtTheLastPixelOfEachEdge(int x, int y, int ex, int ey)
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        Assert.Equal((ex, ey), CursorNudge.Clamp(x, y, screen));
    }

    [Theory]
    [InlineData(-1921, 0, -1920, 0)]
    [InlineData(1920, 0, 1919, 0)]
    [InlineData(0, -301, 0, -300)]
    [InlineData(0, 1080, 0, 1079)]
    [InlineData(-100, -100, -100, -100)]
    public void Clamp_HandlesAVirtualScreenWithANegativeOrigin(int x, int y, int ex, int ey)
    {
        // A monitor left of and above the primary one: the desktop starts at (-1920, -300).
        var screen = new VirtualScreen(-1920, -300, 3840, 1380);

        Assert.Equal((ex, ey), CursorNudge.Clamp(x, y, screen));
    }
}
