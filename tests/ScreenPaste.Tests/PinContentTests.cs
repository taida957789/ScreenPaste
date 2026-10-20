using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// What a pin shows (its image, and the text it came from when it came from text), and what
/// a pin closed recoverably keeps so it opens again as and where it was. WPF imaging needs an
/// STA thread, hence <see cref="StaRunner"/>.
/// </summary>
public sealed class PinContentTests
{
    private static BitmapSource Pixel()
    {
        var bmp = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[4], 4);
        bmp.Freeze();
        return bmp;
    }

    [Fact]
    public void Content_FromAnImage_HasNoSourceText()
    {
        StaRunner.Run(() =>
        {
            var image = Pixel();

            var content = new PinContent(image);

            Assert.Same(image, content.Image);
            Assert.Null(content.SourceText);
        });
    }

    [Fact]
    public void Content_FromText_KeepsTheText()
    {
        StaRunner.Run(() =>
        {
            var content = new PinContent(Pixel(), "hello");

            Assert.Equal("hello", content.SourceText);
        });
    }

    [Fact]
    public void ClosedPin_KeepsWhatItShowed_WhereItWas_AndHowItWasTurned()
    {
        StaRunner.Run(() =>
        {
            // Arrange
            var content = new PinContent(Pixel());
            var state = PinTransformState.Identity.RotateClockwise().ZoomBy(2);
            var settings = new AppSettings();

            // Act
            var closed = new ClosedPin(content, -120, 340, state, settings);

            // Assert
            Assert.Same(content, closed.Content);
            Assert.Equal((-120, 340), (closed.Left, closed.Top));
            Assert.Equal(state, closed.State);
            Assert.Same(settings, closed.Settings);
        });
    }
}
