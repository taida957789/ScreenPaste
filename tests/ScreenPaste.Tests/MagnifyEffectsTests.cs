using System.Windows;
using System.Windows.Media;
using ScreenPaste.Editor;
using ScreenPaste.Rendering;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The UI-free geometry behind a magnifier annotation. Anything needing a live visual tree
/// (Create/Resample/ApplySpec) is covered by running the app, not here.
/// </summary>
public sealed class MagnifyEffectsTests
{
    private static MagnifySpec Spec(Rect source, double zoom = 2.5, ShapeKind shape = ShapeKind.RoundedRectangle) =>
        new(source, shape, zoom, BorderThickness: 3, BorderColor: Colors.Red,
            Connector: true, Shadow: true, Smooth: true, IncludeAnnotations: true);

    [Theory]
    [InlineData(100, 50, 2.5, 250, 125)]
    [InlineData(70, 34, 3, 210, 102)]
    [InlineData(40, 24, 10, 400, 240)]
    public void ViewSize_IsTheFramedSourceScaledByZoom(
        double w, double h, double zoom, double expectedW, double expectedH)
    {
        var size = MagnifyEffects.ViewSize(Spec(new Rect(0, 0, w, h), zoom));

        Assert.Equal(expectedW, size.Width);
        Assert.Equal(expectedH, size.Height);
    }

    [Fact]
    public void ViewSize_NeverCollapsesBelowAVisibleMinimum()
    {
        var size = MagnifyEffects.ViewSize(Spec(new Rect(0, 0, 1, 1), zoom: 1.2));

        Assert.True(size.Width >= 8);
        Assert.True(size.Height >= 8);
    }

    [Fact]
    public void Place_PutsTheEnlargedViewBesideTheSource_InsideTheSelection()
    {
        var selection = new Rect(0, 0, 420, 300);
        var spec = Spec(new Rect(40, 60, 70, 34));

        var at = MagnifyEffects.Place(spec, selection);
        var size = MagnifyEffects.ViewSize(spec);

        Assert.True(at.X >= selection.X);
        Assert.True(at.Y >= selection.Y);
        Assert.True(at.X + size.Width <= selection.Right);
        Assert.True(at.Y + size.Height <= selection.Bottom);
        Assert.True(at.X >= spec.Source.Right, "prefers the space to the right of the source");
    }

    [Fact]
    public void Place_FallsBackToAClampedPosition_WhenTheViewCannotFitBesideTheSource()
    {
        var selection = new Rect(0, 0, 200, 200);
        var spec = Spec(new Rect(10, 10, 180, 180), zoom: 10);   // view far larger than the selection

        var at = MagnifyEffects.Place(spec, selection);

        Assert.Equal(selection.X, at.X);
        Assert.Equal(selection.Y, at.Y);
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle)]
    [InlineData(ShapeKind.RoundedRectangle)]
    public void EdgePoint_LandsOnTheRectBorder_TowardsTheTarget(ShapeKind shape)
    {
        var r = new Rect(0, 0, 100, 50);

        var p = MagnifyEffects.EdgePoint(r, shape, new Point(500, 25));

        Assert.Equal(r.Right, p.X, 6);     // straight to the right -> exits the east edge
        Assert.Equal(25, p.Y, 6);
    }

    [Fact]
    public void EdgePoint_LandsOnTheEllipseBorder()
    {
        var r = new Rect(0, 0, 100, 50);    // radii 50 x 25, centred at (50,25)

        var p = MagnifyEffects.EdgePoint(r, ShapeKind.Ellipse, new Point(50, 500));

        Assert.Equal(50, p.X, 6);           // straight down -> bottom of the ellipse
        Assert.Equal(r.Bottom, p.Y, 6);
    }

    [Fact]
    public void EdgePoint_ReturnsTheCentre_WhenTheTargetIsTheCentre()
    {
        var r = new Rect(0, 0, 100, 50);

        var p = MagnifyEffects.EdgePoint(r, ShapeKind.Rectangle, new Point(50, 25));

        Assert.Equal(new Point(50, 25), p);
    }
}
