using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Rendering;

namespace ScreenPaste.Output;

/// <summary>
/// The image a pin copies, saves and prints: its pixels with the pin's effects, turned and
/// flipped exactly as the pin shows them, at the image's own resolution. Zoom and opacity
/// are only how the pin is displayed and stay out of it.
/// </summary>
public static class PinRenderer
{
    /// <summary>The effects, then the orientation; the image itself when neither applies.</summary>
    public static BitmapSource Render(BitmapSource source, PinTransformState state) =>
        Orient(Effects(source, state), state);

    /// <summary>The image with the state's grayscale and invert.</summary>
    public static BitmapSource Effects(BitmapSource source, PinTransformState state) =>
        PixelEffects.Apply(source, state.Grayscale, state.Invert);

    /// <summary>The image turned and flipped by the state's <see cref="PinTransformState.Orientation"/>,
    /// whose exact quarter-turn and mirror entries TransformedBitmap takes as they are.</summary>
    public static BitmapSource Orient(BitmapSource image, PinTransformState state)
    {
        var orientation = state.Orientation;
        if (orientation.IsIdentity) return image;
        var turned = new TransformedBitmap(image, new MatrixTransform(orientation));
        turned.Freeze();
        return turned;
    }
}
