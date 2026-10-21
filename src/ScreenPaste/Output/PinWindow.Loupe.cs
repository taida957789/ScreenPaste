using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Editor;
using ScreenPaste.Input;
using ScreenPaste.Native;

namespace ScreenPaste.Output;

/// <summary>
/// A pin's magnifier and colour picker (Snipaste): an Alt tap turns it on and off. While it
/// is on it follows the cursor over the pin (<see cref="PinLoupe"/>), C copies the colour
/// under the cursor as it is written and a Shift tap cycles the format (HEX, RGB, HSV), as in
/// the capture; W/A/S/D move the cursor a pixel at a time to pick exactly. A tap is a
/// modifier pressed and released on its own (<see cref="ModifierTapTracker"/>), so Alt+F4, a
/// Shift+drag or Ctrl+Alt+C toggle nothing. The colours are the image's as the pin shows it
/// (turned, flipped, grey or inverted) at its own resolution, not the screen's scaled pixels.
/// It goes off when the pin loses the focus, as the keys then go elsewhere.
/// </summary>
public sealed partial class PinWindow
{
    private readonly ModifierTapTracker _modifierTaps = new();
    private PinLoupe? _loupe;
    private bool _loupeOn;
    private ColorFormatKind _loupeFormat = ColorFormatKind.Hex;
    private Color? _loupeColour;               // the colour under the cursor, while the loupe shows one
    private ShownPixels? _shownPixels;         // the shown image's pixels, kept while the loupe is on

    /// <summary>The image as shown (<see cref="RenderedImage"/>) in 32-bit BGRA, with what it was made from.</summary>
    private sealed record ShownPixels(BitmapSource Effected, Matrix Orientation, BitmapSource Image, byte[] Bgra, int Stride)
    {
        public Color ColourAt(int x, int y)
        {
            int i = y * Stride + x * 4;
            return Color.FromRgb(Bgra[i + 2], Bgra[i + 1], Bgra[i]);
        }
    }

    private void InitLoupe()
    {
        // A mouse button, the wheel or losing the focus between a modifier's down and up means
        // it was part of a gesture (Shift+drag, Ctrl+wheel), not a tap.
        PreviewMouseDown += (_, _) => _modifierTaps.Cancel();
        PreviewMouseUp += (_, _) => _modifierTaps.Cancel();
        PreviewMouseWheel += (_, _) => _modifierTaps.Cancel();
        Deactivated += (_, _) =>
        {
            _modifierTaps.Cancel();
            SetLoupe(false);
        };
        MouseLeave += (_, _) =>
        {
            if (!IsMouseCaptured) HideLoupe();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) HideLoupe();
        };
        Closed += (_, _) =>
        {
            _loupe?.Close();
            _loupe = null;
            _shownPixels = null;
        };
    }

    /// <summary>The loupe is on and shows a pixel, so C has a colour to copy.</summary>
    private bool LoupeShowing => _loupeOn && _loupeColour != null;

    private void SetLoupe(bool on)
    {
        _loupeOn = on;
        if (on)
        {
            UpdateLoupe();
            return;
        }
        HideLoupe();
        _shownPixels = null;   // a large image's copy is not kept for nothing
    }

    private void HideLoupe()
    {
        _loupeColour = null;
        _loupe?.Hide();
    }

    /// <summary>Show the pixel under the cursor, or hide the loupe while the cursor is off the image.</summary>
    private void UpdateLoupe()
    {
        if (!_loupeOn || _closed || !IsVisible) return;
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        var cursor = new Point(pt.X, pt.Y);
        if (PixelAtCursor(cursor) is not { } pixel)
        {
            HideLoupe();
            return;
        }
        var shown = CurrentShownPixels();
        var colour = shown.ColourAt(pixel.X, pixel.Y);
        _loupeColour = colour;
        _loupe ??= new PinLoupe();
        _loupe.ShowPixel(shown.Image, pixel.X, pixel.Y, colour, ColorFormat.Format(colour, _loupeFormat), cursor);
    }

    /// <summary>The pixel of the image as turned under <paramref name="cursor"/>, in the
    /// whole pin or in a thumbnail's part of it; null off the image.</summary>
    private (int X, int Y)? PixelAtCursor(Point cursor)
    {
        var shownRect = ImageRect();
        var (sx, sy) = DisplayScale();
        var (w, h) = TurnedSourceSize();
        var part = _thumbnail?.Crop ?? new Rect(0, 0, w, h);
        return PinGeometry.PixelAt(cursor - (Vector)shownRect.TopLeft, sx, sy, part);
    }

    /// <summary>The shown image's pixels, made again only when the content, the turn, a flip
    /// or an effect has changed it.</summary>
    private ShownPixels CurrentShownPixels()
    {
        var effected = Effected(_state);
        var orientation = _state.Orientation;
        if (_shownPixels is { } kept && ReferenceEquals(kept.Effected, effected) && kept.Orientation == orientation)
            return kept;

        var bgra = new FormatConvertedBitmap(PinRenderer.Orient(effected, _state), PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        var bytes = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(bytes, stride, 0);
        var image = BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
        image.Freeze();
        _shownPixels = new ShownPixels(effected, orientation, image, bytes, stride);
        return _shownPixels;
    }

    /// <summary>C: the colour under the cursor, as the loupe writes it. Carries on when another
    /// app keeps the clipboard locked, as the pin's copy does.</summary>
    private void CopyLoupeColour()
    {
        if (_loupeColour is { } colour) ClipboardService.CopyText(ColorFormat.Format(colour, _loupeFormat));
    }

    /// <summary>A Shift tap: the next colour format, HEX, RGB, HSV.</summary>
    private void CycleLoupeFormat()
    {
        _loupeFormat = ColorFormat.Next(_loupeFormat);
        UpdateLoupe();
    }

    private static bool AnyMouseButtonDown() =>
        Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed ||
        Mouse.MiddleButton == MouseButtonState.Pressed;
}
