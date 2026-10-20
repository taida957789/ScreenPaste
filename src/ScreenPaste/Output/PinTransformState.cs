using System.Windows.Media;

namespace ScreenPaste.Output;

/// <summary>
/// How a pin shows its image (Snipaste's pin keys): zoom, a quarter-turn rotation, flips,
/// opacity, grayscale and invert, plus what a middle-click reset replaced so the next one
/// brings it back. Immutable: every change returns a new state, so <see cref="PinWindow"/>
/// can compare the old and the new one to decide what to move. Pure, so it is unit tested.
///
/// Rotation and flips act on what is on screen: a flip mirrors the pin as it is shown, after
/// its rotation, and a turn turns the pin as it is shown, mirrored or not.
/// </summary>
public sealed record PinTransformState
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 8.0;
    public const double MinOpacity = 0.1;
    public const double MaxOpacity = 1.0;
    /// <summary>One Ctrl+wheel notch or Ctrl++ / Ctrl+- press.</summary>
    public const double OpacityStep = 0.1;

    // Tolerance for "on the opacity grid" and "at 100%" after floating-point steps.
    private const double Epsilon = 1e-9;

    public static PinTransformState Identity { get; } = new();

    public double Zoom { get; init; } = 1.0;
    /// <summary>Clockwise degrees on screen: 0, 90, 180 or 270.</summary>
    public int Rotation { get; init; }
    /// <summary>Mirrored left to right, after the rotation.</summary>
    public bool FlipHorizontal { get; init; }
    /// <summary>Mirrored top to bottom, after the rotation.</summary>
    public bool FlipVertical { get; init; }
    public double Opacity { get; init; } = 1.0;
    public bool Grayscale { get; init; }
    public bool Invert { get; init; }
    /// <summary>The zoom and opacity the last reset replaced, which the next one restores;
    /// null once either changes again.</summary>
    public (double Zoom, double Opacity)? BeforeReset { get; init; }

    /// <summary>Turned a quarter, so the pin is as wide as the image is tall.</summary>
    public bool QuarterTurned => Rotation is 90 or 270;

    /// <summary>Exactly one flip: the pin shows the image's mirror picture.</summary>
    public bool IsMirrored => FlipHorizontal != FlipVertical;

    /// <summary>At 100% size and fully opaque.</summary>
    public bool IsAtRest => Math.Abs(Zoom - 1.0) < Epsilon && Math.Abs(Opacity - 1.0) < Epsilon;

    /// <summary>
    /// The rotation, then the flips, as a screen matrix (y down; WPF's row vectors, so
    /// "first A, then B" is A * B), with exact 0 and ±1 entries for TransformedBitmap and
    /// the pin's layout transform. No translation.
    /// </summary>
    public Matrix Orientation
    {
        get
        {
            var (m11, m12, m21, m22) = Rotation switch
            {
                90 => (0.0, 1.0, -1.0, 0.0),
                180 => (-1.0, 0.0, 0.0, -1.0),
                270 => (0.0, -1.0, 1.0, 0.0),
                _ => (1.0, 0.0, 0.0, 1.0),
            };
            // Flipping a result axis negates that output column.
            double sx = FlipHorizontal ? -1 : 1, sy = FlipVertical ? -1 : 1;
            return new Matrix(m11 * sx, m12 * sy, m21 * sx, m22 * sy, 0, 0);
        }
    }

    /// <summary>1: turn what is shown a quarter clockwise. Mirrored, the image underneath
    /// turns the other way.</summary>
    public PinTransformState RotateClockwise() => Turned(IsMirrored ? -90 : 90);

    /// <summary>2: turn what is shown a quarter counter-clockwise.</summary>
    public PinTransformState RotateCounterClockwise() => Turned(IsMirrored ? 90 : -90);

    /// <summary>3</summary>
    public PinTransformState FlipHorizontally() => this with { FlipHorizontal = !FlipHorizontal };

    /// <summary>4</summary>
    public PinTransformState FlipVertically() => this with { FlipVertical = !FlipVertical };

    /// <summary>5</summary>
    public PinTransformState ToggleGrayscale() => this with { Grayscale = !Grayscale };

    /// <summary>6</summary>
    public PinTransformState ToggleInvert() => this with { Invert = !Invert };

    /// <summary>The zoom, kept within 10%-800%; a change forgets what a reset saved.</summary>
    public PinTransformState WithZoom(double zoom)
    {
        double z = Math.Clamp(zoom, MinZoom, MaxZoom);
        return z == Zoom ? this : this with { Zoom = z, BeforeReset = null };
    }

    /// <summary>The wheel, + and -: multiply the zoom.</summary>
    public PinTransformState ZoomBy(double factor) => WithZoom(Zoom * factor);

    /// <summary>The opacity, kept within 10%-100%; a change forgets what a reset saved.</summary>
    public PinTransformState WithOpacity(double opacity)
    {
        double o = Math.Clamp(opacity, MinOpacity, MaxOpacity);
        return o == Opacity ? this : this with { Opacity = o, BeforeReset = null };
    }

    /// <summary>Ctrl+wheel, Ctrl++ and Ctrl+-: the next 10% step up (+1) or down (-1); from
    /// between two steps, the nearer one in that direction.</summary>
    public PinTransformState WithOpacityStep(int direction)
    {
        if (direction == 0) return this;
        double steps = Opacity / OpacityStep;
        double next = direction > 0 ? Math.Floor(steps + Epsilon) + 1 : Math.Ceiling(steps - Epsilon) - 1;
        return WithOpacity(Math.Round(next * OpacityStep, 2));
    }

    /// <summary>The menu's reset: 100% size and opacity, remembering what it replaced. At rest
    /// already, it keeps what an earlier reset remembered.</summary>
    public PinTransformState Reset() => IsAtRest
        ? this
        : this with { Zoom = 1.0, Opacity = 1.0, BeforeReset = (Zoom, Opacity) };

    /// <summary>Middle-click: reset to 100%, or, right after a reset, go back to what it
    /// replaced. Rotation, flips and effects stay as they are.</summary>
    public PinTransformState ToggleReset() => IsAtRest && BeforeReset is { } saved
        ? this with { Zoom = saved.Zoom, Opacity = saved.Opacity, BeforeReset = null }
        : Reset();

    private PinTransformState Turned(int degrees) => this with { Rotation = ((Rotation + degrees) % 360 + 360) % 360 };
}
