using System.Windows;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Where a pin sits and how big it is, in physical screen px: the image's size once turned
/// and zoomed, and the rect a zoom moves it to so the image point under the cursor stays
/// put (Snipaste's wheel zoom), or a quarter turn so the centre does.
/// </summary>
public sealed class PinGeometryTests
{
    private static readonly Rect Window = new(100, 100, 200, 100);

    // ---------------------------------------------------------------- output size ---

    [Theory]
    [InlineData(0, 200, 100)]
    [InlineData(90, 100, 200)]
    [InlineData(180, 200, 100)]
    [InlineData(270, 100, 200)]
    public void OutputPixelSize_SwapsTheSidesOnAQuarterTurn(int rotation, int width, int height)
    {
        var size = PinGeometry.OutputPixelSize(200, 100, rotation, 1.0);

        Assert.Equal((width, height), size);
    }

    [Fact]
    public void OutputPixelSize_ScalesByTheZoom()
    {
        Assert.Equal((300, 150), PinGeometry.OutputPixelSize(200, 100, 0, 1.5));
        Assert.Equal((50, 100), PinGeometry.OutputPixelSize(200, 100, 90, 0.5));
    }

    [Fact]
    public void OutputPixelSize_IsNeverBelowOnePixel()
    {
        Assert.Equal((1, 1), PinGeometry.OutputPixelSize(3, 3, 0, 0.1));
    }

    [Fact]
    public void PixelScale_FillsTheRoundedOutputExactly()
    {
        // Arrange: 201x101 turned a quarter and halved shows as 50x100 (rounded).
        var (w, h) = PinGeometry.OutputPixelSize(201, 101, 90, 0.5);

        // Act
        var (sx, sy) = PinGeometry.PixelScale(201, 101, 90, 0.5);

        // Assert: the turned image (101 wide, 201 tall) is stretched onto exactly that.
        Assert.Equal(w, 101 * sx, 9);
        Assert.Equal(h, 201 * sy, 9);
    }

    // ---------------------------------------------------------------- zoom at ---

    [Fact]
    public void ZoomAt_KeepsTheImagePointUnderTheCursorStill()
    {
        // Arrange: the cursor a quarter of the way in on both axes.
        var cursor = new Point(150, 125);

        // Act
        var zoomed = PinGeometry.ZoomAt(Window, cursor, 1.0, 2.0);

        // Assert
        Assert.Equal(new Rect(50, 75, 400, 200), zoomed);
        Assert.Equal(0.25, (cursor.X - zoomed.X) / zoomed.Width, 10);
        Assert.Equal(0.25, (cursor.Y - zoomed.Y) / zoomed.Height, 10);
    }

    [Fact]
    public void ZoomAt_ZoomingOut_ShrinksTowardsTheCursor()
    {
        var zoomed = PinGeometry.ZoomAt(Window, new Point(300, 200), 2.0, 1.0);

        Assert.Equal(new Rect(200, 150, 100, 50), zoomed);
    }

    [Fact]
    public void ZoomAt_WithTheCursorLeftOfThePin_KeepsTheLeftEdge()
    {
        var zoomed = PinGeometry.ZoomAt(Window, new Point(0, 150), 1.0, 2.0);

        Assert.Equal(100, zoomed.X);
        Assert.Equal(50, zoomed.Y);
        Assert.Equal(400, zoomed.Width);
    }

    [Fact]
    public void ZoomAt_WithTheCursorBeyondACorner_KeepsThatCorner()
    {
        var zoomed = PinGeometry.ZoomAt(Window, new Point(5000, 5000), 1.0, 0.5);

        Assert.Equal(Window.BottomRight, zoomed.BottomRight);
        Assert.Equal(100, zoomed.Width);
    }

    [Fact]
    public void ZoomAt_TheSameZoom_LeavesThePinWhereItIs()
    {
        Assert.Equal(Window, PinGeometry.ZoomAt(Window, new Point(130, 140), 1.5, 1.5));
    }

    [Fact]
    public void ZoomAt_FromNoZoom_LeavesThePinWhereItIs()
    {
        Assert.Equal(Window, PinGeometry.ZoomAt(Window, new Point(130, 140), 0, 2));
    }

    // ---------------------------------------------------------------- recentre ---

    [Fact]
    public void Recentre_KeepsTheCentre()
    {
        var turned = PinGeometry.Recentre(Window, 100, 200);

        Assert.Equal(new Rect(150, 50, 100, 200), turned);
        Assert.Equal(new Point(200, 150), new Point(turned.X + turned.Width / 2, turned.Y + turned.Height / 2));
    }

    [Fact]
    public void Recentre_TheSameSize_KeepsTheTopLeft()
    {
        Assert.Equal(Window, PinGeometry.Recentre(Window, 200, 100));
    }

    // ---------------------------------------------------------------- next top-left ---

    [Fact]
    public void NextTopLeft_AZoomAtTheTopLeft_KeepsTheTopLeftExactly()
    {
        // 6 px at 110% would be 6.6 px but rounds to 6: recentring on the rounded size would
        // move the top-left by 0.3 px, a drift that adds up over the wheel notches.
        var window = new Rect(100, 100, 6, 6);

        var topLeft = PinGeometry.NextTopLeft(window, window.TopLeft, 1.1, 1.21, 6, 6, quarterTurn: false);

        Assert.Equal(new Point(100, 100), topLeft);
    }

    [Fact]
    public void NextTopLeft_AZoomAtTheCursor_KeepsTheImagePointUnderIt()
    {
        var topLeft = PinGeometry.NextTopLeft(Window, new Point(150, 120), 1.0, 2.0, 400, 200, quarterTurn: false);

        Assert.Equal(new Point(50, 80), topLeft);
    }

    [Fact]
    public void NextTopLeft_AQuarterTurn_KeepsTheCentre()
    {
        var topLeft = PinGeometry.NextTopLeft(Window, new Point(0, 0), 1.0, 1.0, 100, 200, quarterTurn: true);

        Assert.Equal(new Point(150, 50), topLeft);
    }

    [Fact]
    public void NextTopLeft_NoZoomNoTurn_LeavesThePinWhereItIs()
    {
        var topLeft = PinGeometry.NextTopLeft(Window, new Point(130, 140), 1.5, 1.5, 200, 100, quarterTurn: false);

        Assert.Equal(Window.TopLeft, topLeft);
    }
}
