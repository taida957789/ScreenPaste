using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Shift+drag of the region (and later of an annotation) keeps the move on one axis: the
/// one the pointer has travelled further along.
/// </summary>
public sealed class AxisLockTests
{
    [Theory]
    [InlineData(30, 10, 30, 0)]
    [InlineData(-30, 10, -30, 0)]
    [InlineData(30, -29.5, 30, 0)]
    public void Apply_KeepsTheHorizontalMove_WhenItIsLonger(double dx, double dy, double ex, double ey)
    {
        Assert.Equal((ex, ey), AxisLock.Apply(dx, dy));
    }

    [Theory]
    [InlineData(10, 30, 0, 30)]
    [InlineData(10, -30, 0, -30)]
    [InlineData(-0.5, 1, 0, 1)]
    public void Apply_KeepsTheVerticalMove_WhenItIsLonger(double dx, double dy, double ex, double ey)
    {
        Assert.Equal((ex, ey), AxisLock.Apply(dx, dy));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(-5, 5)]
    [InlineData(5, -5)]
    public void Apply_PrefersTheHorizontalMove_OnATie(double dx, double dy)
    {
        Assert.Equal((dx, 0d), AxisLock.Apply(dx, dy));
    }

    [Fact]
    public void Apply_WithNoMovement_StaysPut()
    {
        Assert.Equal((0d, 0d), AxisLock.Apply(0, 0));
    }
}
