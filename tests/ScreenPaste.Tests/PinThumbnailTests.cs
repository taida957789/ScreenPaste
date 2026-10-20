using System.Windows;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A pin's thumbnail mode (Snipaste's Shift+double-click, and the right-drag selection
/// thumbnail): which part of the shown image it keeps, in image px of the image as turned,
/// how large it shows, where it sits and where the full pin comes back. Screen positions
/// are physical px.
/// </summary>
public sealed class PinThumbnailTests
{
    // ---------------------------------------------------------------- crop ---

    [Theory]
    [InlineData(200, 100, 64, 64, 50, 0, 100, 100)]    // wide image, square thumb
    [InlineData(100, 300, 64, 64, 0, 100, 100, 100)]   // tall image
    [InlineData(100, 100, 2, 1, 0, 25, 100, 50)]       // square image, wide thumb
    [InlineData(300, 150, 64, 32, 0, 0, 300, 150)]     // the same shape: the whole image
    public void CropRect_IsTheLargestCentredPartOfTheThumbsShape(
        int imageW, int imageH, int thumbW, int thumbH, double x, double y, double w, double h)
    {
        var crop = PinThumbnail.CropRect(new Size(imageW, imageH), new Size(thumbW, thumbH));

        Assert.Equal(new Rect(x, y, w, h), crop);
    }

    [Fact]
    public void CropRect_StaysOnWholePixels()
    {
        // 101 px to spare: the crop starts at 50, not 50.5.
        var crop = PinThumbnail.CropRect(new Size(201, 100), new Size(64, 64));

        Assert.Equal(new Rect(50, 0, 100, 100), crop);
    }

    [Fact]
    public void CropRect_WithoutAThumbShape_IsTheWholeImage()
    {
        var crop = PinThumbnail.CropRect(new Size(200, 100), new Size(0, 0));

        Assert.Equal(new Rect(0, 0, 200, 100), crop);
    }

    [Fact]
    public void CropRect_OfAnEmptyImage_IsEmpty()
    {
        Assert.True(PinThumbnail.CropRect(new Size(0, 0), new Size(64, 64)).IsEmpty);
    }

    // ---------------------------------------------------------------- square thumbnail ---

    [Fact]
    public void Square_ShowsTheCentredSquareAtTheThumbnailSide()
    {
        var view = PinThumbnail.Square(400, 200, 1.0, 64);

        Assert.Equal(new Rect(100, 0, 200, 200), view.Crop);
        Assert.Equal(0.32, view.Scale, 6);
        Assert.Equal((64, 64), view.OutputSize);
    }

    [Fact]
    public void Square_NeverGrowsAPinShownSmallerThanTheThumbnail()
    {
        // 100x40 at 50% shows as 50x20: the thumbnail is 20 px, not 64.
        var view = PinThumbnail.Square(100, 40, 0.5, 64);

        Assert.Equal(new Rect(30, 0, 40, 40), view.Crop);
        Assert.Equal((20, 20), view.OutputSize);
    }

    [Fact]
    public void OutputSize_RoundsAndIsNeverBelowOnePixel()
    {
        Assert.Equal((13, 13), new PinThumbnailView(new Rect(0, 0, 10, 10), 1.26).OutputSize);
        Assert.Equal((1, 1), new PinThumbnailView(new Rect(0, 0, 2, 2), 0.1).OutputSize);
    }

    // ---------------------------------------------------------------- selection thumbnail ---

    [Fact]
    public void FromDrag_TakesTheDraggedPartBackIntoImagePixels()
    {
        // Dragged over the pin shown at 200%.
        var crop = PinThumbnail.FromDrag(new Rect(20, 10, 40, 30), 2.0, 100, 50);

        Assert.Equal(new Rect(10, 5, 20, 15), crop);
    }

    [Fact]
    public void FromDrag_TakesEveryPixelTheDragTouches()
    {
        var crop = PinThumbnail.FromDrag(new Rect(3, 3, 4, 4), 2.0, 100, 50);

        Assert.Equal(new Rect(1, 1, 3, 3), crop);
    }

    [Fact]
    public void FromDrag_StaysInsideTheImage()
    {
        var crop = PinThumbnail.FromDrag(new Rect(-10, -10, 1000, 1000), 1.0, 100, 50);

        Assert.Equal(new Rect(0, 0, 100, 50), crop);
    }

    [Fact]
    public void FromDrag_KeepsAtLeastOnePixel()
    {
        Assert.Equal(new Rect(10, 10, 1, 1), PinThumbnail.FromDrag(new Rect(10, 10, 0, 0), 1.0, 100, 50));
        Assert.Equal(new Rect(99, 49, 1, 1), PinThumbnail.FromDrag(new Rect(100, 50, 0, 0), 1.0, 100, 50));
    }

    [Theory]
    [InlineData(3, 3, false)]
    [InlineData(-3, 2, false)]
    [InlineData(4, 0, true)]
    [InlineData(0, -4, true)]
    public void IsDrag_NeedsFourPixelsAlongAnAxis(double dx, double dy, bool expected)
    {
        Assert.Equal(expected, PinThumbnail.IsDrag(new Vector(dx, dy)));
    }

    // ---------------------------------------------------------------- placement ---

    [Fact]
    public void PlaceAt_CentresTheThumbnailOnTheCursor()
    {
        var at = PinThumbnail.PlaceAt(new Rect(100, 100, 400, 300), new Point(300, 250), 64, 64);

        Assert.Equal(new Point(268, 218), at);
    }

    [Theory]
    [InlineData(105, 105, 100, 100)]
    [InlineData(495, 395, 436, 336)]
    public void PlaceAt_KeepsTheThumbnailOverThePin(double cx, double cy, double x, double y)
    {
        var at = PinThumbnail.PlaceAt(new Rect(100, 100, 400, 300), new Point(cx, cy), 64, 64);

        Assert.Equal(new Point(x, y), at);
    }

    [Fact]
    public void RestoreTopLeft_PutsAnUnmovedThumbnailsPinBackWhereItWas()
    {
        var back = PinThumbnail.RestoreTopLeft(new Point(100, 100), new Point(268, 218), new Point(268, 218));

        Assert.Equal(new Point(100, 100), back);
    }

    [Fact]
    public void RestoreTopLeft_MovesThePinAsFarAsTheThumbnailWasDragged()
    {
        var back = PinThumbnail.RestoreTopLeft(new Point(100, 100), new Point(268, 218), new Point(300, 200));

        Assert.Equal(new Point(132, 82), back);
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(0, PinThumbnail.MinSide)]
    [InlineData(100000, PinThumbnail.MaxSide)]
    public void ClampSide_KeepsTheSettingInRange(int setting, int expected)
    {
        Assert.Equal(expected, PinThumbnail.ClampSide(setting));
    }
}
