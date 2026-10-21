using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Rendering;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// A pin's grayscale (5) and invert (6) on BGRA bytes. Pins hold premultiplied pixels
/// (Pbgra32: every colour channel is at most its alpha), so invert is c' = a - c and the
/// alpha never changes.
/// </summary>
public sealed class PixelEffectsTests
{
    // ---------------------------------------------------------------- grayscale ---

    [Fact]
    public void Grayscale_OpaqueRed_BecomesItsLuminance()
    {
        var gray = PixelEffects.Grayscale([0, 0, 255, 255]);

        Assert.Equal(new byte[] { 77, 77, 77, 255 }, gray);
    }

    [Fact]
    public void Grayscale_KeepsWhiteAndBlack()
    {
        var gray = PixelEffects.Grayscale([255, 255, 255, 255, 0, 0, 0, 255]);

        Assert.Equal(new byte[] { 255, 255, 255, 255, 0, 0, 0, 255 }, gray);
    }

    [Fact]
    public void Grayscale_PreservesAlpha_AndStaysPremultiplied()
    {
        // Arrange: half-transparent white, premultiplied.
        byte[] pixels = [128, 128, 128, 128, 0, 0, 0, 0];

        // Act
        var gray = PixelEffects.Grayscale(pixels);

        // Assert: colour never exceeds alpha; a transparent pixel stays transparent black.
        Assert.Equal(new byte[] { 128, 128, 128, 128, 0, 0, 0, 0 }, gray);
    }

    [Fact]
    public void Grayscale_LeavesItsInputAlone()
    {
        byte[] pixels = [10, 20, 30, 255];

        PixelEffects.Grayscale(pixels);

        Assert.Equal(new byte[] { 10, 20, 30, 255 }, pixels);
    }

    // ---------------------------------------------------------------- invert ---

    [Fact]
    public void Invert_Opaque_IsTheUsualNegative()
    {
        var inverted = PixelEffects.Invert([10, 20, 30, 255]);

        Assert.Equal(new byte[] { 245, 235, 225, 255 }, inverted);
    }

    [Fact]
    public void Invert_Premultiplied_SubtractsFromAlpha()
    {
        var inverted = PixelEffects.Invert([10, 20, 30, 128]);

        Assert.Equal(new byte[] { 118, 108, 98, 128 }, inverted);
    }

    [Fact]
    public void Invert_TransparentPixel_StaysTransparentBlack()
    {
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, PixelEffects.Invert([0, 0, 0, 0]));
    }

    [Fact]
    public void InvertTwice_ReturnsTheOriginal()
    {
        byte[] pixels = [10, 20, 30, 255, 1, 64, 100, 128, 0, 0, 0, 0, 7, 7, 7, 7];

        var twice = PixelEffects.Invert(PixelEffects.Invert(pixels));

        Assert.Equal(pixels, twice);
    }

    [Fact]
    public void Invert_LeavesItsInputAlone()
    {
        byte[] pixels = [10, 20, 30, 255];

        PixelEffects.Invert(pixels);

        Assert.Equal(new byte[] { 10, 20, 30, 255 }, pixels);
    }

    [Fact]
    public void Effects_RejectAPartialPixel()
    {
        Assert.Throws<ArgumentException>(() => PixelEffects.Invert([1, 2, 3]));
        Assert.Throws<ArgumentException>(() => PixelEffects.Grayscale([1, 2, 3, 4, 5]));
    }

    // ---------------------------------------------------------------- bitmaps ---

    private static byte[] PixelsOf(BitmapSource bmp)
    {
        var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(px, bmp.PixelWidth * 4, 0);
        return px;
    }

    [Fact]
    public void Apply_ToAStraightAlphaImage_WorksOnItsPremultipliedPixels()
    {
        StaRunner.Run(() =>
        {
            // Arrange: half-transparent red with straight alpha (Bgra32).
            var src = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 128 }, 4);

            // Act
            var inverted = PixelEffects.Apply(src, grayscale: false, invert: true);

            // Assert: premultiplied (0,0,128,128) inverted is half-transparent cyan.
            Assert.Equal(PixelFormats.Pbgra32, inverted.Format);
            Assert.Equal(new byte[] { 128, 128, 0, 128 }, PixelsOf(inverted));
            Assert.True(inverted.IsFrozen);
        });
    }

    [Fact]
    public void Apply_BothEffects_GraysThenInverts()
    {
        StaRunner.Run(() =>
        {
            var src = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[] { 0, 0, 255, 255 }, 4);

            var result = PixelEffects.Apply(src, grayscale: true, invert: true);

            Assert.Equal(new byte[] { 178, 178, 178, 255 }, PixelsOf(result));
            Assert.Equal(new Int32Rect(0, 0, 1, 1), new Int32Rect(0, 0, result.PixelWidth, result.PixelHeight));
        });
    }

    [Fact]
    public void Apply_NoEffect_ReturnsTheSourceItself()
    {
        StaRunner.Run(() =>
        {
            var src = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[] { 1, 2, 3, 255 }, 4);

            Assert.Same(src, PixelEffects.Apply(src, grayscale: false, invert: false));
        });
    }
}
