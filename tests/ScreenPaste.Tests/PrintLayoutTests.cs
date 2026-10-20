using System.Windows;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Placing a capture on the printed page (Ctrl+P). Units are WPF's 1/96 inch, so a pixel
/// printed at 100% is one unit: a capture is shrunk to fit the printable area, keeping its
/// aspect ratio, never enlarged past 100%, and centred.
/// </summary>
public sealed class PrintLayoutTests
{
    private const double Tolerance = 1e-9;
    private static readonly Rect A4Portrait = new(0, 0, 793.7, 1122.5);

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Width, actual.Width, Tolerance);
        Assert.Equal(expected.Height, actual.Height, Tolerance);
    }

    // ---------------------------------------------------------------- fit ---

    [Fact]
    public void FitToPage_ShrinksALandscapeCapture_ToTheAreaWidth_CentredVertically()
    {
        var area = new Rect(0, 0, 800, 1100);

        var placed = PrintLayout.FitToPage(new Size(2000, 1000), area);

        AssertRect(new Rect(0, 350, 800, 400), placed);
    }

    [Fact]
    public void FitToPage_ShrinksAPortraitCapture_ToTheAreaHeight_CentredHorizontally()
    {
        var area = new Rect(0, 0, 800, 1100);

        var placed = PrintLayout.FitToPage(new Size(1000, 2200), area);

        AssertRect(new Rect(150, 0, 500, 1100), placed);
    }

    [Fact]
    public void FitToPage_OnALandscapePage_UsesTheWiderSide()
    {
        var landscape = new Rect(0, 0, 1100, 800);

        var placed = PrintLayout.FitToPage(new Size(2200, 1000), landscape);

        AssertRect(new Rect(0, 150, 1100, 500), placed);
    }

    [Fact]
    public void FitToPage_NeverEnlargesASmallCapture_PastOneHundredPercent()
    {
        var area = new Rect(0, 0, 800, 1100);

        var placed = PrintLayout.FitToPage(new Size(200, 100), area);

        AssertRect(new Rect(300, 500, 200, 100), placed);
    }

    [Fact]
    public void FitToPage_CentresWithinAnInsetPrintableArea()
    {
        // The printer's margins: the printable area does not start at the page corner.
        var area = new Rect(48, 36, 700, 1000);

        var placed = PrintLayout.FitToPage(new Size(100, 100), area);

        AssertRect(new Rect(348, 486, 100, 100), placed);
    }

    [Fact]
    public void FitToPage_KeepsTheAspectRatio_AndStaysInsideTheArea()
    {
        var image = new Size(1919, 1081);

        var placed = PrintLayout.FitToPage(image, A4Portrait);

        Assert.Equal(image.Width / image.Height, placed.Width / placed.Height, 1e-6);
        Assert.True(A4Portrait.Contains(placed));
    }

    [Fact]
    public void FitToPage_ACaptureThatExactlyFits_PrintsAtOneHundredPercent()
    {
        var area = new Rect(10, 20, 640, 480);

        var placed = PrintLayout.FitToPage(new Size(640, 480), area);

        AssertRect(area, placed);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    public void FitToPage_AnEmptyCapture_PlacesNothing(double width, double height)
    {
        var placed = PrintLayout.FitToPage(new Size(width, height), new Rect(0, 0, 800, 1100));

        Assert.Equal(0, placed.Width);
        Assert.Equal(0, placed.Height);
    }

    [Fact]
    public void FitToPage_AnEmptyPrintableArea_PlacesNothing()
    {
        var placed = PrintLayout.FitToPage(new Size(640, 480), new Rect(5, 5, 0, 0));

        Assert.Equal(0, placed.Width);
        Assert.Equal(0, placed.Height);
    }

    // ---------------------------------------------------------------- orientation ---

    [Theory]
    [InlineData(1920, 1080, true)]
    [InlineData(1080, 1920, false)]
    [InlineData(500, 500, false)]
    public void IsLandscape_OnlyForCapturesWiderThanTall(double width, double height, bool expected)
    {
        Assert.Equal(expected, PrintLayout.IsLandscape(new Size(width, height)));
    }

    // ---------------------------------------------------------------- imageable area ---

    [Fact]
    public void Imageable_KeepsAReportedAreaThatLiesOnThePage()
    {
        var reported = new Rect(18, 18, 757, 1086);

        Assert.Equal(reported, PrintLayout.Imageable(A4Portrait.Size, reported));
    }

    [Fact]
    public void Imageable_TurnsAnAreaReportedForTheOtherOrientation()
    {
        // Some drivers report the portrait area even after the page was turned landscape.
        var landscape = new Size(1122.5, 793.7);

        var area = PrintLayout.Imageable(landscape, new Rect(18, 24, 757, 1080));

        AssertRect(new Rect(24, 18, 1080, 757), area);
    }

    [Fact]
    public void Imageable_FallsBackToTheWholePage_WhenTheAreaFitsNeitherWay()
    {
        var area = PrintLayout.Imageable(A4Portrait.Size, new Rect(0, 0, 2000, 2000));

        Assert.Equal(new Rect(A4Portrait.Size), area);
    }

    [Fact]
    public void Imageable_FallsBackToTheWholePage_WhenNothingUsableWasReported()
    {
        var area = PrintLayout.Imageable(A4Portrait.Size, new Rect(0, 0, 0, 0));

        Assert.Equal(new Rect(A4Portrait.Size), area);
    }

    [Fact]
    public void Imageable_TrustsTheReportedArea_WhenThePageSizeIsUnknown()
    {
        var reported = new Rect(18, 18, 757, 1086);

        Assert.Equal(reported, PrintLayout.Imageable(new Size(0, 0), reported));
    }
}
