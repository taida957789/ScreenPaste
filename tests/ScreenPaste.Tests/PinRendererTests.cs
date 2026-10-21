using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The image a pin copies, saves and prints: its pixels with the pin's effects and turned and
/// flipped exactly as it is shown, at the image's own resolution (zoom and opacity are only
/// how the pin is displayed). WPF imaging needs an STA thread, hence <see cref="StaRunner"/>.
/// </summary>
public sealed class PinRendererTests
{
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Green = Color.FromRgb(0, 255, 0);
    private static readonly Color White = Color.FromRgb(255, 255, 255);

    /// <summary>A bitmap from rows of opaque colours, top row first.</summary>
    private static BitmapSource Image(params Color[][] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var pixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = (y * w + x) * 4;
            pixels[i] = rows[y][x].B;
            pixels[i + 1] = rows[y][x].G;
            pixels[i + 2] = rows[y][x].R;
            pixels[i + 3] = 255;
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private static Color PixelAt(BitmapSource bmp, int x, int y)
    {
        var px = new byte[4];
        new FormatConvertedBitmap(bmp, PixelFormats.Pbgra32, null, 0).CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return Color.FromArgb(px[3], px[2], px[1], px[0]);
    }

    [Fact]
    public void Render_Untouched_IsTheImageItself()
    {
        StaRunner.Run(() =>
        {
            var image = Image([Red, Blue]);

            Assert.Same(image, PinRenderer.Render(image, PinTransformState.Identity.WithZoom(3).WithOpacity(0.5)));
        });
    }

    [Fact]
    public void Render_TurnedClockwise_PutsTheLeftEdgeOnTop()
    {
        StaRunner.Run(() =>
        {
            var turned = PinRenderer.Render(Image([Red, Blue]), PinTransformState.Identity.RotateClockwise());

            Assert.Equal(1, turned.PixelWidth);
            Assert.Equal(2, turned.PixelHeight);
            Assert.Equal(Red, PixelAt(turned, 0, 0));
            Assert.Equal(Blue, PixelAt(turned, 0, 1));
        });
    }

    [Fact]
    public void Render_TurnedCounterClockwise_PutsTheLeftEdgeAtTheBottom()
    {
        StaRunner.Run(() =>
        {
            var turned = PinRenderer.Render(Image([Red, Blue]), PinTransformState.Identity.RotateCounterClockwise());

            Assert.Equal(Blue, PixelAt(turned, 0, 0));
            Assert.Equal(Red, PixelAt(turned, 0, 1));
        });
    }

    [Fact]
    public void Render_FlippedHorizontally_SwapsLeftAndRight()
    {
        StaRunner.Run(() =>
        {
            var flipped = PinRenderer.Render(Image([Red, Blue]), PinTransformState.Identity.FlipHorizontally());

            Assert.Equal(Blue, PixelAt(flipped, 0, 0));
            Assert.Equal(Red, PixelAt(flipped, 1, 0));
        });
    }

    [Fact]
    public void Render_TurnedThenFlipped_MatchesWhatThePinShows()
    {
        StaRunner.Run(() =>
        {
            // Arrange: R B / G W. A clockwise turn shows G R / W B; flipping that horizontally
            // shows R G / B W (the image mirrored along its diagonal).
            var image = Image([Red, Blue], [Green, White]);

            // Act
            var shown = PinRenderer.Render(image, PinTransformState.Identity.RotateClockwise().FlipHorizontally());

            // Assert
            Assert.Equal(Red, PixelAt(shown, 0, 0));
            Assert.Equal(Green, PixelAt(shown, 1, 0));
            Assert.Equal(Blue, PixelAt(shown, 0, 1));
            Assert.Equal(White, PixelAt(shown, 1, 1));
        });
    }

    [Fact]
    public void Render_WithEffectsAndATurn_AppliesBoth()
    {
        StaRunner.Run(() =>
        {
            var state = PinTransformState.Identity.ToggleInvert().RotateClockwise();

            var shown = PinRenderer.Render(Image([Red, Blue]), state);

            Assert.Equal(Color.FromRgb(0, 255, 255), PixelAt(shown, 0, 0));
            Assert.Equal(Color.FromRgb(255, 255, 0), PixelAt(shown, 0, 1));
        });
    }
}
