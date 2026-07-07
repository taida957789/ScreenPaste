using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Rendering;

/// <summary>
/// Builds the visual for a blur region. The visual embeds the screenshot pixels
/// under the region so it both previews live and bakes correctly at export.
/// </summary>
public static class BlurEffects
{
    /// <summary>Gaussian blur via WPF's GPU BlurEffect.</summary>
    public static FrameworkElement CreateGaussian(BitmapSource screenshot, Int32Rect regionPx, double radius)
    {
        var crop = Crop(screenshot, regionPx);
        var img = new Image
        {
            Source = crop,
            Width = regionPx.Width,
            Height = regionPx.Height,
            Stretch = Stretch.Fill,
            Effect = new BlurEffect
            {
                Radius = Math.Max(0.1, radius),
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Performance,
            },
        };
        return img;
    }

    /// <summary>Mosaic/pixelate: downscale then nearest-neighbour upscale.</summary>
    public static FrameworkElement CreateMosaic(BitmapSource screenshot, Int32Rect regionPx, double block)
    {
        var crop = Crop(screenshot, regionPx);
        double b = Math.Max(2, block);
        double scale = 1.0 / b;

        var small = new TransformedBitmap(crop, new ScaleTransform(scale, scale));
        small.Freeze();

        var img = new Image
        {
            Source = small,
            Width = regionPx.Width,
            Height = regionPx.Height,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        return img;
    }

    private static BitmapSource Crop(BitmapSource src, Int32Rect r)
    {
        // Clamp to source bounds so CroppedBitmap never throws on edge regions.
        int x = Math.Clamp(r.X, 0, src.PixelWidth - 1);
        int y = Math.Clamp(r.Y, 0, src.PixelHeight - 1);
        int w = Math.Clamp(r.Width, 1, src.PixelWidth - x);
        int h = Math.Clamp(r.Height, 1, src.PixelHeight - y);
        var cropped = new CroppedBitmap(src, new Int32Rect(x, y, w, h));
        cropped.Freeze();
        return cropped;
    }
}
