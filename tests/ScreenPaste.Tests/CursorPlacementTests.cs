using System.Windows;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// ` / ! show the mouse pointer as it was when the capture began. Its image is drawn with the
/// hotspot (the arrow's tip) where the pointer was, and only the part over the area reaches
/// the snip. The image itself is recovered from two renderings, on black and on white, which
/// is where the alpha a GDI draw loses comes back from.
/// </summary>
public sealed class CursorPlacementTests
{
    // ---------------------------------------------------------------- hotspot ---

    [Fact]
    public void TopLeft_MovesTheImageUpAndLeftByItsHotspot()
    {
        var (x, y) = CursorPlacement.TopLeft(pointerX: 500, pointerY: 300, hotspotX: 11, hotspotY: 7);

        Assert.Equal(489, x);
        Assert.Equal(293, y);
    }

    [Fact]
    public void TopLeft_WithTheHotspotInTheCorner_IsThePointerItself()
    {
        Assert.Equal((-1200, 40), CursorPlacement.TopLeft(-1200, 40, 0, 0));
    }

    [Fact]
    public void LocalTo_MapsTheScreenPositionIntoTheOverlay()
    {
        var screen = new VirtualScreen(-1920, -200, 3840, 1280);

        var local = CursorPlacement.LocalTo(-1000, 100, screen);

        Assert.Equal(new Point(920, 300), local);
    }

    // ---------------------------------------------------------------- in the area ---

    [Fact]
    public void InRegion_InsideTheArea_IsRegionLocal()
    {
        var at = CursorPlacement.InRegion(new Point(150, 120), 32, 32, new Int32Rect(100, 100, 400, 300));

        Assert.Equal(new Rect(50, 20, 32, 32), at);
    }

    [Fact]
    public void InRegion_PartlyOutsideTheTopLeft_StillLands_HangingOverTheEdge()
    {
        var at = CursorPlacement.InRegion(new Point(90, 95), 32, 32, new Int32Rect(100, 100, 400, 300));

        Assert.Equal(new Rect(-10, -5, 32, 32), at);
    }

    [Fact]
    public void InRegion_PartlyOutsideTheBottomRight_StillLands()
    {
        var at = CursorPlacement.InRegion(new Point(490, 390), 32, 32, new Int32Rect(100, 100, 400, 300));

        Assert.Equal(new Rect(390, 290, 32, 32), at);
    }

    [Theory]
    [InlineData(50, 50)]     // above and to the left
    [InlineData(500, 150)]   // just past the right edge
    [InlineData(150, 400)]   // just below the bottom edge
    [InlineData(68, 150)]    // its right edge touches the area's left edge
    public void InRegion_EntirelyOutside_IsNothing(double x, double y)
    {
        Assert.Null(CursorPlacement.InRegion(new Point(x, y), 32, 32, new Int32Rect(100, 100, 400, 300)));
    }

    [Fact]
    public void InRegion_AnEmptyImage_IsNothing()
    {
        Assert.Null(CursorPlacement.InRegion(new Point(150, 150), 0, 32, new Int32Rect(100, 100, 400, 300)));
    }

    // ---------------------------------------------------------------- alpha ---

    private static byte[] Bgr(params (byte B, byte G, byte R)[] pixels) =>
        pixels.SelectMany(p => new byte[] { p.B, p.G, p.R, 0 }).ToArray();   // GDI leaves alpha at 0

    [Fact]
    public void RecoverAlpha_AnOpaquePixel_LooksTheSameOnBothBackgrounds()
    {
        var onBlack = Bgr((10, 20, 30));
        var onWhite = Bgr((10, 20, 30));

        var pbgra = CursorSnapshot.RecoverAlpha(onBlack, onWhite);

        Assert.Equal(new byte[] { 10, 20, 30, 255 }, pbgra);
    }

    [Fact]
    public void RecoverAlpha_AClearPixel_IsTheBackgroundItself()
    {
        var pbgra = CursorSnapshot.RecoverAlpha(Bgr((0, 0, 0)), Bgr((255, 255, 255)));

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, pbgra);
    }

    [Fact]
    public void RecoverAlpha_AHalfShadow_KeepsItsAlpha_Premultiplied()
    {
        // 50% black: c*a + bg*(1-a) gives 0 on black and 128 on white.
        var pbgra = CursorSnapshot.RecoverAlpha(Bgr((0, 0, 0)), Bgr((128, 128, 128)));

        Assert.Equal(new byte[] { 0, 0, 0, 127 }, pbgra);
    }

    [Fact]
    public void RecoverAlpha_AnInvertingPixel_BecomesOpaqueWhite()
    {
        // A monochrome cursor's XOR part: white over black, black over white.
        var pbgra = CursorSnapshot.RecoverAlpha(Bgr((255, 255, 255)), Bgr((0, 0, 0)));

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pbgra);
    }

    [Fact]
    public void RecoverAlpha_NeverLetsAColourExceedItsAlpha()
    {
        // Rounding in the GDI blend can leave a channel a step above what the alpha allows.
        var pbgra = CursorSnapshot.RecoverAlpha(Bgr((130, 0, 0)), Bgr((255, 128, 128)));

        Assert.True(pbgra[0] <= pbgra[3]);
        Assert.Equal(127, pbgra[3]);
    }

    // ---------------------------------------------------------------- the real pointer ---

    /// <summary>Reads the live pointer, so what it finds depends on the machine: on a desktop
    /// session a frozen 96-DPI image of the pointer's size, and nothing at all (never an
    /// exception) when the pointer is hidden or there is no desktop.</summary>
    [Fact]
    public void Capture_GivesAFrozenPointerImage_OrNothingWhenThePointerIsHidden()
    {
        var snapshot = CursorSnapshot.Capture();

        if (snapshot == null) return;
        Assert.True(snapshot.Image.IsFrozen);
        Assert.True(snapshot.Image.PixelWidth > 0 && snapshot.Image.PixelHeight > 0);
        Assert.Equal(96, snapshot.Image.DpiX);
    }

    [Fact]
    public void RecoverAlpha_WorksPixelByPixel()
    {
        var pbgra = CursorSnapshot.RecoverAlpha(Bgr((0, 0, 0), (10, 20, 30)), Bgr((255, 255, 255), (10, 20, 30)));

        Assert.Equal(new byte[] { 0, 0, 0, 0, 10, 20, 30, 255 }, pbgra);
    }
}
