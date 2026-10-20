using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The clipboard's raw formats as a pin can take them: a CF_DIB / CF_DIBV5 block becomes a
/// BMP file WIC can decode (the file header points past the info header, the bit masks and
/// the colour table), a DIBV5 whose alpha is all zero is shown opaque rather than invisible,
/// and CF_HTML becomes its plain text. Text and colours render into a pin's bitmap.
/// </summary>
public sealed class ClipboardPinSourceTests
{
    private const int FileHeaderSize = 14;

    /// <summary>A bottom-up DIB of <paramref name="width"/> x 1 with the given header.</summary>
    private static byte[] Dib(int headerSize, ushort bitCount, uint compression, uint colorsUsed, int width, int extra)
    {
        int stride = (width * bitCount + 31) / 32 * 4;
        var dib = new byte[headerSize + extra + stride];
        BitConverter.GetBytes(headerSize).CopyTo(dib, 0);
        BitConverter.GetBytes(width).CopyTo(dib, 4);
        BitConverter.GetBytes(1).CopyTo(dib, 8);
        BitConverter.GetBytes((ushort)1).CopyTo(dib, 12);
        BitConverter.GetBytes(bitCount).CopyTo(dib, 14);
        BitConverter.GetBytes(compression).CopyTo(dib, 16);
        BitConverter.GetBytes(colorsUsed).CopyTo(dib, 32);
        return dib;
    }

    private static int PixelOffset(byte[] file) => BitConverter.ToInt32(file, 10);

    // ---------------------------------------------------------------- DIB to BMP ---

    [Fact]
    public void A32BitDib_GetsAFileHeaderPointingPastTheInfoHeader()
    {
        var dib = Dib(40, 32, 0, 0, 2, 0);

        var file = ClipboardPinSource.BmpFileFromDib(dib);

        Assert.NotNull(file);
        Assert.Equal((byte)'B', file[0]);
        Assert.Equal((byte)'M', file[1]);
        Assert.Equal(FileHeaderSize + dib.Length, BitConverter.ToInt32(file, 2));
        Assert.Equal(FileHeaderSize + 40, PixelOffset(file));
        Assert.Equal(dib, file[FileHeaderSize..]);
    }

    [Fact]
    public void BitfieldsAfterAPlainInfoHeader_AreSkippedToo()
    {
        var file = ClipboardPinSource.BmpFileFromDib(Dib(40, 32, 3, 0, 2, 12));

        Assert.Equal(FileHeaderSize + 40 + 12, PixelOffset(file!));
    }

    [Fact]
    public void AV5Header_CarriesItsMasksInside()
    {
        var file = ClipboardPinSource.BmpFileFromDib(Dib(124, 32, 3, 0, 2, 0));

        Assert.Equal(FileHeaderSize + 124, PixelOffset(file!));
    }

    [Fact]
    public void An8BitDib_SkipsItsColourTable()
    {
        Assert.Equal(FileHeaderSize + 40 + 256 * 4, PixelOffset(ClipboardPinSource.BmpFileFromDib(Dib(40, 8, 0, 0, 4, 256 * 4))!));
        Assert.Equal(FileHeaderSize + 40 + 16 * 4, PixelOffset(ClipboardPinSource.BmpFileFromDib(Dib(40, 8, 0, 16, 4, 16 * 4))!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(4096)]
    public void AMalformedDib_IsTurnedAway(int headerSize)
    {
        var dib = new byte[64];
        BitConverter.GetBytes(headerSize).CopyTo(dib, 0);

        Assert.Null(ClipboardPinSource.BmpFileFromDib(dib));
    }

    [Fact]
    public void ADibTooShortForItsColourTable_IsTurnedAway()
    {
        var dib = Dib(40, 8, 0, 0, 4, 0);   // claims 256 colours, has none

        Assert.Null(ClipboardPinSource.BmpFileFromDib(dib));
    }

    [Fact]
    public void ARealDib_DecodesThroughTheBmpFile()
    {
        StaRunner.Run(() =>
        {
            var dib = Dib(40, 32, 0, 0, 2, 0);
            dib[40] = 0xFC; dib[41] = 0xA9; dib[42] = 0x3D;   // first pixel, BGR

            var image = ClipboardPinSource.DecodeDib(dib);

            Assert.NotNull(image);
            Assert.Equal(2, image.PixelWidth);
            Assert.Equal(1, image.PixelHeight);
            var bgra = new byte[8];
            new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(bgra, 8, 0);
            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, bgra[..4]);
        });
    }

    // ---------------------------------------------------------------- alpha ---

    [Fact]
    public void PixelsWithNoAlphaAtAll_AreFullyTransparent()
    {
        Assert.True(ClipboardPinSource.IsFullyTransparent(new byte[] { 1, 2, 3, 0, 4, 5, 6, 0 }));
    }

    [Fact]
    public void OneOpaquePixel_IsNotFullyTransparent()
    {
        Assert.False(ClipboardPinSource.IsFullyTransparent(new byte[] { 1, 2, 3, 0, 4, 5, 6, 1 }));
        Assert.False(ClipboardPinSource.IsFullyTransparent(Array.Empty<byte>()));
    }

    // ---------------------------------------------------------------- HTML ---

    [Fact]
    public void Html_TakesTheFragmentAsPlainText()
    {
        const string cfHtml = "Version:0.9\r\nStartHTML:0000000105\r\nEndHTML:0000000200\r\n" +
            "<html><body><!--StartFragment--><b>Hello</b> &amp; <i>bye</i><!--EndFragment--></body></html>";

        Assert.Equal("Hello & bye", ClipboardPinSource.HtmlToText(cfHtml));
    }

    [Theory]
    [InlineData("<p>one</p><p>two</p>", "one\ntwo")]
    [InlineData("a<br>b<br/>c", "a\nb\nc")]
    [InlineData("<style>p { color: red }</style>text", "text")]
    [InlineData("<script>alert(1)</script>safe", "safe")]
    [InlineData("<ul><li>x</li><li>y</li></ul>", "x\ny")]
    [InlineData("&lt;tag&gt; &quot;q&quot;", "<tag> \"q\"")]
    [InlineData("", "")]
    public void Html_KeepsLinesAndDropsMarkup(string html, string expected)
    {
        Assert.Equal(expected, ClipboardPinSource.HtmlToText(html));
    }

    // ---------------------------------------------------------------- rendering ---

    [Fact]
    public void Text_RendersWithinTheMaximumWidth()
    {
        StaRunner.Run(() =>
        {
            var image = TextPinRenderer.RenderText(string.Join(" ", Enumerable.Repeat("wrapping words", 200)), 1.0);

            Assert.InRange(image.PixelWidth, 1, (int)TextPinRenderer.MaxWidthDip);
            Assert.True(image.PixelHeight > 40);
            Assert.True(image.IsFrozen);
        });
    }

    [Fact]
    public void Text_ScalesWithTheMonitor()
    {
        StaRunner.Run(() =>
        {
            var small = TextPinRenderer.RenderText("Hello", 1.0);
            var large = TextPinRenderer.RenderText("Hello", 2.0);

            Assert.InRange(large.PixelWidth, small.PixelWidth * 2 - 4, small.PixelWidth * 2 + 4);
        });
    }

    [Fact]
    public void AHugeText_StaysWithinTheMaximumHeight()
    {
        StaRunner.Run(() =>
        {
            var image = TextPinRenderer.RenderText(string.Join("\n", Enumerable.Repeat("line", 50_000)), 1.0);

            Assert.True(image.PixelHeight <= TextPinRenderer.MaxHeightDip + 2 * TextPinRenderer.PaddingDip + 2);
        });
    }

    [Fact]
    public void AColour_RendersASwatchOfIt()
    {
        StaRunner.Run(() =>
        {
            var image = TextPinRenderer.RenderColor(Color.FromRgb(0x3D, 0xA9, 0xFC), 1.0);
            var bgra = new byte[4];
            new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0)
                .CopyPixels(new System.Windows.Int32Rect(image.PixelWidth / 2, 10, 1, 1), bgra, 4, 0);

            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, bgra);
        });
    }

    // ---------------------------------------------------------------- resolve ---

    /// <summary>A 2x1 opaque image: the first pixel #3DA9FC, the second white.</summary>
    private static BitmapSource TwoPixels()
    {
        var pixels = new byte[] { 0xFC, 0xA9, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var bmp = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
        bmp.Freeze();
        return bmp;
    }

    private static MemoryStream Png(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }

    private static byte[] FirstPixel(BitmapSource image)
    {
        var bgra = new byte[4];
        new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(0, 0, 1, 1), bgra, 4, 0);
        return bgra;
    }

    private static string TempPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "sp-test-" + Guid.NewGuid().ToString("N") + ".png");
        using var file = File.Create(path);
        Png(TwoPixels()).CopyTo(file);
        return path;
    }

    [Fact]
    public void Resolve_APngStream_PinsTheImage()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(ClipboardFormatPicker.PngFormat, Png(TwoPixels()));
            data.SetData(DataFormats.UnicodeText, "ignored: the image comes first");

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.NotNull(content);
            Assert.Equal(2, content.Image.PixelWidth);
            Assert.Null(content.SourceText);
            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, FirstPixel(content.Image));
        });
    }

    [Fact]
    public void Resolve_ABrokenPngStream_FallsBackToTheText()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(ClipboardFormatPicker.PngFormat, new MemoryStream(new byte[] { 1, 2, 3 }));
            data.SetData(DataFormats.UnicodeText, "plain words");

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal("plain words", content?.SourceText);
        });
    }

    [Fact]
    public void Resolve_ADib_PinsTheImage()
    {
        StaRunner.Run(() =>
        {
            var dib = Dib(40, 32, 0, 0, 2, 0);
            dib[40] = 0xFC; dib[41] = 0xA9; dib[42] = 0x3D;
            var data = new DataObject();
            data.SetData(DataFormats.Dib, new MemoryStream(dib));

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, FirstPixel(content!.Image));
        });
    }

    [Fact]
    public void Resolve_ADibV5WithNoAlpha_IsShownOpaque()
    {
        StaRunner.Run(() =>
        {
            // A V5 header with BI_BITFIELDS and an alpha mask, every alpha byte zero.
            var dib = Dib(124, 32, 3, 0, 2, 0);
            BitConverter.GetBytes(0x00FF0000u).CopyTo(dib, 40);
            BitConverter.GetBytes(0x0000FF00u).CopyTo(dib, 44);
            BitConverter.GetBytes(0x000000FFu).CopyTo(dib, 48);
            BitConverter.GetBytes(0xFF000000u).CopyTo(dib, 52);
            dib[124] = 0xFC; dib[125] = 0xA9; dib[126] = 0x3D;
            var data = new DataObject();
            data.SetData(ClipboardFormatPicker.DibV5Format, new MemoryStream(dib));

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.NotNull(content);
            Assert.Equal(0xFF, FirstPixel(content.Image)[3]);
        });
    }

    [Fact]
    public void Resolve_ABitmap_PinsACopyOfIt()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.Bitmap, TwoPixels());

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.NotNull(content);
            Assert.True(content.Image.IsFrozen);
            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, FirstPixel(content.Image));
        });
    }

    [Fact]
    public void Resolve_ACopiedImageFile_PinsTheImage()
    {
        StaRunner.Run(() =>
        {
            var path = TempPng();
            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { path });

                var content = ClipboardPinSource.Resolve(data, 1.0);

                Assert.Equal(2, content?.Image.PixelWidth);
                Assert.Null(content?.SourceText);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void Resolve_ACopiedPathToAnImage_PinsTheImage()
    {
        StaRunner.Run(() =>
        {
            var path = TempPng();
            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.UnicodeText, "\"" + path + "\"");

                var content = ClipboardPinSource.Resolve(data, 1.0);

                Assert.Equal(2, content?.Image.PixelWidth);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void Resolve_FilesThatAreNotImages_PinTheirNames()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { @"C:\docs\report.docx", @"C:\docs\notes.txt" });

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal(@"C:\docs\report.docx" + Environment.NewLine + @"C:\docs\notes.txt", content?.SourceText);
        });
    }

    [Fact]
    public void Resolve_AColourValue_PinsAColourCardThatKeepsTheText()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, "  #3DA9FC\r\n");

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal("#3DA9FC", content?.SourceText);
            var swatch = new byte[4];
            new FormatConvertedBitmap(content!.Image, PixelFormats.Bgra32, null, 0)
                .CopyPixels(new Int32Rect(content.Image.PixelWidth / 2, 10, 1, 1), swatch, 4, 0);
            Assert.Equal(new byte[] { 0xFC, 0xA9, 0x3D, 0xFF }, swatch);
        });
    }

    [Fact]
    public void Resolve_Text_PinsTheTextAndKeepsItWhole()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, "第一行\r\nsecond line");

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal("第一行\r\nsecond line", content?.SourceText);
            Assert.True(content!.Image.PixelHeight > 20);
        });
    }

    [Fact]
    public void Resolve_HtmlAlone_PinsItsText()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.Html, "<p>Hello</p><p>world</p>");

            var content = ClipboardPinSource.Resolve(data, 1.0);

            Assert.Equal("Hello\nworld", content?.SourceText);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n ")]
    public void Resolve_BlankText_PinsNothing(string text)
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);

            Assert.Null(ClipboardPinSource.Resolve(data, 1.0));
        });
    }

    [Fact]
    public void Resolve_NothingPinnable_PinsNothing()
    {
        StaRunner.Run(() =>
        {
            var data = new DataObject();
            data.SetData("Some Private Format", new byte[] { 1, 2, 3 });

            Assert.Null(ClipboardPinSource.Resolve(data, 1.0));
        });
    }
}
