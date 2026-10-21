using System.Windows;
using ScreenPaste.Capture;
using ScreenPaste.Editor;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The custom snip dialog's request (Snipaste's Shift+F1): an area in physical screen px
/// and a delay. Validate keeps the part of the area that is on screen and turns away one
/// with no size or entirely off screen; the delay is clamped to 0-60 seconds. The snip
/// modes say how a capture finishes once its area is chosen.
/// </summary>
public sealed class CustomSnipRequestTests
{
    // Two monitors: 1920x1080 at the origin, and one to the left of it.
    private static readonly VirtualScreen Screen = new(-1920, 0, 3840, 1080);

    [Fact]
    public void AnAreaOnScreen_IsKept()
    {
        var request = CustomSnipRequest.Validate(new Int32Rect(100, 200, 640, 480), 3, Screen);

        Assert.NotNull(request);
        Assert.Equal(new Int32Rect(100, 200, 640, 480), request.Region);
        Assert.Equal(3, request.DelaySeconds);
    }

    [Fact]
    public void AnAreaOnTheLeftMonitor_IsKept()
    {
        var request = CustomSnipRequest.Validate(new Int32Rect(-1900, 10, 300, 200), 0, Screen);

        Assert.Equal(new Int32Rect(-1900, 10, 300, 200), request?.Region);
    }

    [Fact]
    public void AnAreaPartlyOffScreen_IsCutToTheScreen()
    {
        var request = CustomSnipRequest.Validate(new Int32Rect(1800, 1000, 400, 300), 0, Screen);

        Assert.Equal(new Int32Rect(1800, 1000, 120, 80), request?.Region);
    }

    [Fact]
    public void AnAreaLargerThanTheScreen_BecomesTheScreen()
    {
        var request = CustomSnipRequest.Validate(new Int32Rect(-5000, -5000, 20000, 20000), 0, Screen);

        Assert.Equal(new Int32Rect(-1920, 0, 3840, 1080), request?.Region);
    }

    [Theory]
    [InlineData(5000, 100, 100, 100)]    // right of the screen
    [InlineData(0, -500, 100, 100)]      // above it
    [InlineData(1920, 0, 100, 100)]      // just past the right edge
    public void AnAreaOffScreen_IsTurnedAway(int x, int y, int w, int h)
    {
        Assert.Null(CustomSnipRequest.Validate(new Int32Rect(x, y, w, h), 0, Screen));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-10, 100)]
    [InlineData(100, -10)]
    public void AnAreaWithNoSize_IsTurnedAway(int w, int h)
    {
        Assert.Null(CustomSnipRequest.Validate(new Int32Rect(10, 10, w, h), 0, Screen));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2.5, 2.5)]
    [InlineData(60, 60)]
    [InlineData(61, 60)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 60)]
    public void TheDelay_IsClampedToAMinute(double delay, double expected)
    {
        var request = CustomSnipRequest.Validate(new Int32Rect(10, 10, 100, 100), delay, Screen);

        Assert.Equal(expected, request?.DelaySeconds);
    }

    [Fact]
    public void TheDelay_ClampsTheSameWayOnItsOwn()
    {
        Assert.Equal(0, CustomSnipRequest.ClampDelay(-3));
        Assert.Equal(1.5, CustomSnipRequest.ClampDelay(1.5));
        Assert.Equal(CustomSnipRequest.MaxDelaySeconds, CustomSnipRequest.ClampDelay(999));
    }

    // ---------------------------------------------------------------- snip modes ---

    [Theory]
    [InlineData(CaptureMode.Normal, OutputAction.None)]
    [InlineData(CaptureMode.Copy, OutputAction.Copy)]
    [InlineData(CaptureMode.Pin, OutputAction.Pin)]
    [InlineData(CaptureMode.CopyAndPin, OutputAction.CopyAndPin)]
    [InlineData(CaptureMode.LastAreaCopy, OutputAction.Copy)]
    [InlineData(CaptureMode.FullScreenCopy, OutputAction.Copy)]
    [InlineData(CaptureMode.ActiveWindow, OutputAction.None)]
    [InlineData(CaptureMode.ActiveScreen, OutputAction.None)]
    public void EachMode_FinishesWithItsOutput(CaptureMode mode, OutputAction expected)
    {
        Assert.Equal(expected, CaptureModes.FinishAction(mode));
    }

    [Fact]
    public void ANormalRequest_HasNoModeOrPresetArea()
    {
        var request = new CaptureRequest
        {
            Screenshot = null!,
            Screen = Screen,
            Regions = new SnipRegionHistory(),
            Records = new SnipRecordStore<SnipRecord>(0, 0),
        };

        Assert.Equal(CaptureMode.Normal, request.Mode);
        Assert.Null(request.PresetRegion);
    }
}
