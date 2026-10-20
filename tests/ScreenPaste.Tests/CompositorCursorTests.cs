using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Rendering;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The compositor's snip-context layers: the captured pointer drawn into the base of the
/// snip (under every annotation, clipped to the area), and a replayed snip's flattened area
/// pasted back over its screenshot. WPF imaging needs an STA thread, hence
/// <see cref="StaRunner"/>.
/// </summary>
public sealed class CompositorCursorTests
{
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Red = Color.FromRgb(255, 0, 0);

    private static BitmapSource Solid(int w, int h, Color c)
    {
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = c.B;
            pixels[i + 1] = c.G;
            pixels[i + 2] = c.R;
            pixels[i + 3] = 255;
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private static Color PixelAt(BitmapSource bmp, int x, int y)
    {
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return Color.FromArgb(px[3], px[2], px[1], px[0]);
    }

    [Fact]
    public void CropRegion_WithACursorHangingOverTheTopLeft_DrawsOnlyTheOverlap()
    {
        StaRunner.Run(() =>
        {
            // Arrange: an 8x8 screen, the area (2,2)-(6,6), a 3x3 pointer at (1,1).
            var cursor = new CursorLayer(Solid(3, 3, Red), new Point(1, 1));

            // Act
            var crop = Compositor.CropRegion(Solid(8, 8, Blue), new Int32Rect(2, 2, 4, 4), cursor);

            // Assert: the pointer covers area pixels (0,0)-(1,1), the rest is screen.
            Assert.Equal(4, crop.PixelWidth);
            Assert.Equal(Red, PixelAt(crop, 0, 0));
            Assert.Equal(Red, PixelAt(crop, 1, 1));
            Assert.Equal(Blue, PixelAt(crop, 2, 2));
            Assert.Equal(Blue, PixelAt(crop, 3, 0));
        });
    }

    [Fact]
    public void CropRegion_WithTheCursorElsewhere_IsTheBareScreenshot()
    {
        StaRunner.Run(() =>
        {
            var cursor = new CursorLayer(Solid(2, 2, Red), new Point(6, 6));

            var crop = Compositor.CropRegion(Solid(8, 8, Blue), new Int32Rect(0, 0, 4, 4), cursor);

            Assert.Equal(Blue, PixelAt(crop, 3, 3));
        });
    }

    [Fact]
    public void Compose_DrawsTheCursorUnderTheAnnotations()
    {
        StaRunner.Run(() =>
        {
            // Arrange: a pointer at the area's origin, and a white shape over half of it.
            var cursor = new CursorLayer(Solid(4, 4, Red), new Point(0, 0));
            var shapes = new Canvas();
            var cover = new System.Windows.Shapes.Rectangle { Width = 2, Height = 4, Fill = Brushes.White };
            shapes.Children.Add(cover);
            shapes.Measure(new Size(8, 8));
            shapes.Arrange(new Rect(0, 0, 8, 8));

            // Act
            var img = Compositor.Compose(Solid(8, 8, Blue), new Int32Rect(0, 0, 8, 8), cursor,
                new StrokeCollection(), new Canvas(), new Canvas(), shapes, new Canvas(), new Canvas());

            // Assert
            Assert.Equal(Colors.White, PixelAt(img, 1, 1));   // the shape wins
            Assert.Equal(Red, PixelAt(img, 3, 1));            // the pointer shows beside it
            Assert.Equal(Blue, PixelAt(img, 6, 6));
        });
    }

    [Fact]
    public void Compose_WithoutACursor_IsTheScreenshot()
    {
        StaRunner.Run(() =>
        {
            var img = Compositor.Compose(Solid(4, 4, Blue), new Int32Rect(0, 0, 4, 4), null,
                new StrokeCollection(), new Canvas(), new Canvas(), new Canvas(), new Canvas(), new Canvas());

            Assert.Equal(Blue, PixelAt(img, 0, 0));
        });
    }

    [Fact]
    public void PasteRegion_PutsTheFlattenedAreaBackOverItsScreenshot()
    {
        StaRunner.Run(() =>
        {
            // Act
            var backdrop = Compositor.PasteRegion(Solid(6, 5, Blue), Solid(2, 3, Red), new Int32Rect(1, 2, 2, 3));

            // Assert
            Assert.Equal(6, backdrop.PixelWidth);
            Assert.Equal(5, backdrop.PixelHeight);
            Assert.True(backdrop.IsFrozen);
            Assert.Equal(Red, PixelAt(backdrop, 1, 2));
            Assert.Equal(Red, PixelAt(backdrop, 2, 4));
            Assert.Equal(Blue, PixelAt(backdrop, 0, 2));
            Assert.Equal(Blue, PixelAt(backdrop, 3, 3));
            Assert.Equal(Blue, PixelAt(backdrop, 1, 1));
        });
    }

    [Fact]
    public void PasteRegion_ClipsAnAreaThatReachesPastTheScreenshot()
    {
        StaRunner.Run(() =>
        {
            var backdrop = Compositor.PasteRegion(Solid(4, 4, Blue), Solid(3, 3, Red), new Int32Rect(2, 2, 3, 3));

            Assert.Equal(Red, PixelAt(backdrop, 3, 3));
            Assert.Equal(Blue, PixelAt(backdrop, 1, 1));
        });
    }

    [Fact]
    public void PasteRegion_ClipsAnAreaThatStartsBeforeTheScreenshot()
    {
        StaRunner.Run(() =>
        {
            var backdrop = Compositor.PasteRegion(Solid(4, 4, Blue), Solid(3, 3, Red), new Int32Rect(-1, -1, 3, 3));

            Assert.Equal(Red, PixelAt(backdrop, 0, 0));
            Assert.Equal(Red, PixelAt(backdrop, 1, 1));
            Assert.Equal(Blue, PixelAt(backdrop, 2, 2));
        });
    }
}
