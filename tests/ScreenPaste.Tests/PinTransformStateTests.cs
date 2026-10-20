using System.Windows;
using System.Windows.Media;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A pin's view state (Snipaste's pin keys): 1/2 turn it a quarter, 3/4 flip what is seen,
/// 5/6 toggle grayscale and invert, the wheel zooms, Ctrl+wheel steps the opacity, and a
/// middle-click resets size and opacity, a second one bringing them back. Orientation is a
/// screen matrix (y down, row vectors: "first A, then B" is A * B).
/// </summary>
public sealed class PinTransformStateTests
{
    private static readonly PinTransformState Start = PinTransformState.Identity;
    private static readonly Matrix QuarterTurn = new(0, 1, -1, 0, 0, 0);   // clockwise on screen
    private static readonly Matrix MirrorX = new(-1, 0, 0, 1, 0, 0);
    private static readonly Matrix MirrorY = new(1, 0, 0, -1, 0, 0);

    private static PinTransformState Flipped(bool horizontal, bool vertical)
    {
        var state = Start;
        if (horizontal) state = state.FlipHorizontally();
        if (vertical) state = state.FlipVertically();
        return state;
    }

    // ---------------------------------------------------------------- rotation ---

    [Fact]
    public void RotateClockwise_StepsAQuarterTurnAtATime()
    {
        var once = Start.RotateClockwise();

        Assert.Equal(90, once.Rotation);
        Assert.Equal(180, once.RotateClockwise().Rotation);
        Assert.Equal(270, once.RotateClockwise().RotateClockwise().Rotation);
    }

    [Fact]
    public void RotateCounterClockwise_FromUpright_WrapsTo270()
    {
        Assert.Equal(270, Start.RotateCounterClockwise().Rotation);
    }

    [Fact]
    public void FourQuarterTurns_EitherWay_ReturnToTheStart()
    {
        var cw = Start.RotateClockwise().RotateClockwise().RotateClockwise().RotateClockwise();
        var ccw = Start.RotateCounterClockwise().RotateCounterClockwise().RotateCounterClockwise().RotateCounterClockwise();

        Assert.Equal(Start, cw);
        Assert.Equal(Start, ccw);
    }

    [Fact]
    public void RotateThenRotateBack_ReturnsToTheStart()
    {
        Assert.Equal(Start, Start.RotateClockwise().RotateCounterClockwise());
    }

    [Fact]
    public void RotateClockwise_TurnsTheRightEdgeDownwards()
    {
        var turned = Start.RotateClockwise().Orientation;

        Assert.Equal(QuarterTurn, turned);
        Assert.Equal(new Point(0, 1), turned.Transform(new Point(1, 0)));
    }

    [Fact]
    public void Identity_HasNoOrientation()
    {
        Assert.True(Start.Orientation.IsIdentity);
        Assert.False(Start.QuarterTurned);
    }

    // ---------------------------------------------------------------- flips ---

    [Fact]
    public void FlippingTwice_ReturnsToTheStart()
    {
        Assert.Equal(Start, Start.FlipHorizontally().FlipHorizontally());
        Assert.Equal(Start, Start.FlipVertically().FlipVertically());
    }

    [Fact]
    public void FlipHorizontally_MirrorsLeftAndRight()
    {
        Assert.Equal(MirrorX, Start.FlipHorizontally().Orientation);
        Assert.Equal(MirrorY, Start.FlipVertically().Orientation);
    }

    [Fact]
    public void BothFlips_LookLikeAHalfTurn()
    {
        var bothFlips = Start.FlipHorizontally().FlipVertically();
        var halfTurn = Start.RotateClockwise().RotateClockwise();

        Assert.Equal(halfTurn.Orientation, bothFlips.Orientation);
    }

    [Fact]
    public void Flip_AfterAQuarterTurn_MirrorsWhatIsSeen()
    {
        var state = Start.RotateClockwise().FlipHorizontally();

        Assert.Equal(QuarterTurn * MirrorX, state.Orientation);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void Rotate_AfterAnyFlips_TurnsWhatIsSeen(bool flipH, bool flipV, bool clockwise)
    {
        // Arrange: a pin already turned once and flipped, then turned again.
        var before = Flipped(flipH, flipV).RotateClockwise();
        var turn = clockwise ? QuarterTurn : QuarterTurn * QuarterTurn * QuarterTurn;

        // Act
        var after = clockwise ? before.RotateClockwise() : before.RotateCounterClockwise();

        // Assert: what was on screen is turned as a whole, mirrored or not.
        Assert.Equal(before.Orientation * turn, after.Orientation);
    }

    [Fact]
    public void FourQuarterTurns_OfAMirroredPin_ReturnToIt()
    {
        var mirrored = Start.FlipHorizontally();

        var turned = mirrored.RotateClockwise().RotateClockwise().RotateClockwise().RotateClockwise();

        Assert.Equal(mirrored, turned);
    }

    [Fact]
    public void QuarterTurned_IsTrueAt90And270Only()
    {
        Assert.True(Start.RotateClockwise().QuarterTurned);
        Assert.False(Start.RotateClockwise().RotateClockwise().QuarterTurned);
        Assert.True(Start.RotateCounterClockwise().QuarterTurned);
    }

    // ---------------------------------------------------------------- effects ---

    [Fact]
    public void ToggleGrayscale_AndInvert_SwitchOnAndOffIndependently()
    {
        var gray = Start.ToggleGrayscale();
        var both = gray.ToggleInvert();

        Assert.True(gray.Grayscale);
        Assert.False(gray.Invert);
        Assert.True(both.Grayscale && both.Invert);
        Assert.Equal(Start, both.ToggleGrayscale().ToggleInvert());
    }

    // ---------------------------------------------------------------- opacity ---

    [Theory]
    [InlineData(1.0, -1, 0.9)]
    [InlineData(0.5, 1, 0.6)]
    [InlineData(0.5, -1, 0.4)]
    public void WithOpacityStep_MovesTenPercent(double from, int direction, double expected)
    {
        var state = Start.WithOpacity(from).WithOpacityStep(direction);

        Assert.Equal(expected, state.Opacity, 10);
    }

    [Fact]
    public void WithOpacityStep_StopsAtTenAndAHundredPercent()
    {
        Assert.Equal(1.0, Start.WithOpacityStep(1).Opacity);
        Assert.Equal(0.1, Start.WithOpacity(0.1).WithOpacityStep(-1).Opacity);
    }

    [Fact]
    public void WithOpacityStep_RepeatedDown_LandsExactlyOnTenPercent()
    {
        var state = Start;
        for (int i = 0; i < 9; i++) state = state.WithOpacityStep(-1);

        Assert.Equal(0.1, state.Opacity);
    }

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(-1, 0.4)]
    public void WithOpacityStep_FromBetweenSteps_GoesToTheNextStep(int direction, double expected)
    {
        Assert.Equal(expected, Start.WithOpacity(0.45).WithOpacityStep(direction).Opacity, 10);
    }

    [Fact]
    public void WithOpacity_IsClampedToTenPercentAndOpaque()
    {
        Assert.Equal(0.1, Start.WithOpacity(0).Opacity);
        Assert.Equal(1.0, Start.WithOpacity(3).Opacity);
    }

    // ---------------------------------------------------------------- zoom ---

    [Fact]
    public void ZoomBy_MultipliesTheZoom()
    {
        Assert.Equal(2.0, Start.ZoomBy(2).Zoom, 10);
        Assert.Equal(0.5, Start.ZoomBy(2).ZoomBy(0.25).Zoom, 10);
    }

    [Fact]
    public void Zoom_IsClampedBetweenTenPercentAndEightTimes()
    {
        Assert.Equal(PinTransformState.MaxZoom, Start.ZoomBy(100).Zoom);
        Assert.Equal(PinTransformState.MinZoom, Start.ZoomBy(0.001).Zoom);
        Assert.Equal(0.1, PinTransformState.MinZoom);
        Assert.Equal(8.0, PinTransformState.MaxZoom);
    }

    [Fact]
    public void ZoomBy_AtTheLimit_ReturnsTheSameState()
    {
        var max = Start.WithZoom(PinTransformState.MaxZoom);

        Assert.Same(max, max.ZoomBy(1.1));
    }

    // ---------------------------------------------------------------- reset ---

    [Fact]
    public void ToggleReset_GoesToAHundredPercent_AndTheNextOneComesBack()
    {
        // Arrange
        var zoomed = Start.WithZoom(2.5).WithOpacity(0.6);

        // Act
        var reset = zoomed.ToggleReset();
        var restored = reset.ToggleReset();

        // Assert
        Assert.Equal(1.0, reset.Zoom);
        Assert.Equal(1.0, reset.Opacity);
        Assert.Equal(2.5, restored.Zoom);
        Assert.Equal(0.6, restored.Opacity);
        Assert.Null(restored.BeforeReset);
    }

    [Fact]
    public void ToggleReset_AtAHundredPercentWithNothingSaved_ChangesNothing()
    {
        Assert.Same(Start, Start.ToggleReset());
    }

    [Fact]
    public void ToggleReset_KeepsTheOrientationAndEffects()
    {
        var state = Start.RotateClockwise().FlipVertically().ToggleInvert().WithZoom(3);

        var reset = state.ToggleReset();

        Assert.Equal(state.Orientation, reset.Orientation);
        Assert.True(reset.Invert);
        Assert.Equal(state.Orientation, reset.ToggleReset().Orientation);
    }

    [Fact]
    public void ZoomingAfterAReset_ReplacesWhatTheResetSaved()
    {
        // Arrange: reset from 2x, then zoom to 1.5x by hand.
        var rezoomed = Start.WithZoom(2).ToggleReset().WithZoom(1.5);

        // Act: the next reset saves 1.5x, and the one after brings 1.5x back, not 2x.
        var reset = rezoomed.ToggleReset();

        // Assert
        Assert.Equal(1.0, reset.Zoom);
        Assert.Equal(1.5, reset.ToggleReset().Zoom);
    }

    [Fact]
    public void ChangingTheOpacityAfterAReset_ForgetsTheSavedValues()
    {
        var state = Start.WithZoom(2).ToggleReset().WithOpacityStep(-1);

        Assert.Null(state.BeforeReset);
    }

    [Fact]
    public void Reset_AtAHundredPercent_KeepsWhatAnEarlierResetSaved()
    {
        var reset = Start.WithZoom(2).Reset();

        var again = reset.Reset();

        Assert.Same(reset, again);
        Assert.Equal(2.0, again.ToggleReset().Zoom);
    }
}
