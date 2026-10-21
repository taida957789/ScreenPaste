using System.Windows;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Annotation geometry: Shift turns a rectangle, ellipse, blur or magnifier drag into a
/// square (circle), and Shift+wheel scales a selected rectangle or ellipse about its centre.
/// </summary>
public sealed class ShapeConstraintsTests
{
    private static readonly Point Start = new(100, 100);

    // ---------------------------------------------------------------- Square ---

    [Theory]
    [InlineData(160, 130, 100, 100)]   // down-right: grows from the start corner
    [InlineData(40, 130, 40, 100)]     // down-left
    [InlineData(160, 70, 100, 40)]     // up-right
    [InlineData(40, 70, 40, 40)]       // up-left
    public void Square_TakesTheLongerSide_InEveryQuadrant(double px, double py, double x, double y)
    {
        var r = ShapeConstraints.Square(Start, new Point(px, py));

        Assert.Equal(new Rect(x, y, 60, 60), r);
    }

    [Fact]
    public void Square_UsesTheVerticalDistance_WhenItIsLonger()
    {
        var r = ShapeConstraints.Square(Start, new Point(110, 180));

        Assert.Equal(new Rect(100, 100, 80, 80), r);
    }

    [Fact]
    public void Square_WithNoMovement_IsEmptyAtTheStart()
    {
        var r = ShapeConstraints.Square(Start, Start);

        Assert.Equal(new Rect(100, 100, 0, 0), r);
    }

    [Fact]
    public void Frame_WithoutTheSquareConstraint_IsThePlainDragRectangle()
    {
        var r = ShapeConstraints.Frame(Start, new Point(40, 130), square: false);

        Assert.Equal(new Rect(40, 100, 60, 30), r);
    }

    [Fact]
    public void Frame_WithTheSquareConstraint_IsTheSquare()
    {
        var p = new Point(40, 130);

        Assert.Equal(ShapeConstraints.Square(Start, p), ShapeConstraints.Frame(Start, p, square: true));
    }

    // ---------------------------------------------------------------- ScaleAbout ---

    private static readonly Rect Box = new(100, 200, 40, 20);
    private const double MinSize = 4;

    private static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    [Fact]
    public void ScaleAbout_GrowsAboutTheCentre()
    {
        var r = ShapeConstraints.ScaleAbout(Box, 1.5, MinSize);

        Assert.Equal(60, r.Width, 9);
        Assert.Equal(30, r.Height, 9);
        Assert.Equal(Center(Box), Center(r));
    }

    [Fact]
    public void ScaleAbout_ShrinksAboutTheCentre()
    {
        var r = ShapeConstraints.ScaleAbout(Box, 0.5, MinSize);

        Assert.Equal(20, r.Width, 9);
        Assert.Equal(10, r.Height, 9);
        Assert.Equal(Center(Box), Center(r));
    }

    [Fact]
    public void ScaleAbout_StopsShrinkingAtTheMinimumSize_KeepingTheAspect()
    {
        var r = ShapeConstraints.ScaleAbout(Box, 0.1, MinSize);

        Assert.Equal(MinSize, r.Height, 9);
        Assert.Equal(MinSize * 2, r.Width, 9);
        Assert.Equal(Center(Box), Center(r));
    }

    [Fact]
    public void ScaleAbout_LeavesAShapeAlreadyAtTheMinimum()
    {
        var small = new Rect(10, 10, 30, 3);

        Assert.Equal(small, ShapeConstraints.ScaleAbout(small, 0.9, MinSize));
    }

    [Fact]
    public void ScaleAbout_CanStillGrowAShapeBelowTheMinimum()
    {
        var small = new Rect(10, 10, 30, 3);

        var r = ShapeConstraints.ScaleAbout(small, 2, MinSize);

        Assert.Equal(60, r.Width, 9);
        Assert.Equal(6, r.Height, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ScaleAbout_IgnoresANonPositiveFactor(double factor)
    {
        Assert.Equal(Box, ShapeConstraints.ScaleAbout(Box, factor, MinSize));
    }
}
