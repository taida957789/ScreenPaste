using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// What a paste tries, in order, from the formats the clipboard offers: a PNG stream, then
/// CF_DIBV5 (both keep the alpha), CF_DIB, the plain bitmap, then the copied files that look
/// like images, then text, then HTML, and last the copied file names themselves as text.
/// ClipboardPinSource goes down the list until one gives a pin.
/// </summary>
public sealed class ClipboardFormatPickerTests
{
    private static ClipboardSource Source(ClipboardSourceKind kind, string? file = null) => new(kind, file);

    [Fact]
    public void AnEmptyClipboard_OffersNothing()
    {
        Assert.Empty(ClipboardFormatPicker.Pick(Array.Empty<string>(), null));
    }

    [Fact]
    public void ABrowserImage_TriesThePngFirst_ThenTheBitmaps()
    {
        var formats = new[] { "Bitmap", "HTML Format", "DeviceIndependentBitmap", "PNG", "Format17", "UnicodeText" };

        var picks = ClipboardFormatPicker.Pick(formats, null);

        Assert.Equal(
            new[]
            {
                Source(ClipboardSourceKind.Png), Source(ClipboardSourceKind.DibV5), Source(ClipboardSourceKind.Dib),
                Source(ClipboardSourceKind.Bitmap), Source(ClipboardSourceKind.Text), Source(ClipboardSourceKind.Html),
            },
            picks);
    }

    [Theory]
    [InlineData("PNG")]
    [InlineData("png")]
    [InlineData("image/png")]
    public void ThePngStream_IsFoundByEitherName(string format)
    {
        Assert.Equal(Source(ClipboardSourceKind.Png), ClipboardFormatPicker.Pick(new[] { format }, null).Single());
    }

    [Fact]
    public void CopiedImageFiles_ComeAfterTheBitmaps_InTheirOrder()
    {
        var files = new[] { @"C:\a\notes.txt", @"C:\a\shot.PNG", @"C:\a\photo.jpeg" };

        var picks = ClipboardFormatPicker.Pick(new[] { "FileDrop", "Bitmap" }, files);

        Assert.Equal(
            new[]
            {
                Source(ClipboardSourceKind.Bitmap),
                Source(ClipboardSourceKind.ImageFile, @"C:\a\shot.PNG"),
                Source(ClipboardSourceKind.ImageFile, @"C:\a\photo.jpeg"),
                Source(ClipboardSourceKind.FileList),
            },
            picks);
    }

    [Theory]
    [InlineData("x.png")]
    [InlineData("x.jpg")]
    [InlineData("x.jpeg")]
    [InlineData("x.bmp")]
    [InlineData("x.gif")]
    [InlineData("x.tif")]
    [InlineData("x.tiff")]
    [InlineData("x.webp")]
    [InlineData("x.ico")]
    public void ImageFiles_AreKnownByTheirExtension(string file)
    {
        Assert.Contains(Source(ClipboardSourceKind.ImageFile, file), ClipboardFormatPicker.Pick(new[] { "FileDrop" }, new[] { file }));
    }

    [Fact]
    public void FilesThatAreNotImages_PasteTheirNamesAsText_AfterAnyText()
    {
        var picks = ClipboardFormatPicker.Pick(new[] { "FileDrop", "UnicodeText" }, new[] { @"C:\a\report.docx" });

        Assert.Equal(new[] { Source(ClipboardSourceKind.Text), Source(ClipboardSourceKind.FileList) }, picks);
    }

    [Fact]
    public void FileNames_WithoutTheFileDropFormat_AreIgnored()
    {
        Assert.Empty(ClipboardFormatPicker.Pick(new[] { "Shell IDList Array" }, new[] { @"C:\a\shot.png" }));
    }

    [Fact]
    public void AnEmptyFileDrop_OffersNoFileList()
    {
        Assert.Empty(ClipboardFormatPicker.Pick(new[] { "FileDrop" }, Array.Empty<string>()));
    }

    [Theory]
    [InlineData("UnicodeText")]
    [InlineData("Text")]
    [InlineData("OEMText")]
    [InlineData("System.String")]
    public void Text_IsFoundByAnyOfItsNames_Once(string format)
    {
        var picks = ClipboardFormatPicker.Pick(new[] { format, "Text" }, null);

        Assert.Equal(new[] { Source(ClipboardSourceKind.Text) }, picks);
    }

    [Fact]
    public void Html_AloneIsStillPasted()
    {
        Assert.Equal(new[] { Source(ClipboardSourceKind.Html) }, ClipboardFormatPicker.Pick(new[] { "HTML Format" }, null));
    }

    [Theory]
    [InlineData("C:\\shots\\a.png", true)]
    [InlineData("  \"C:\\shots\\a.webp\"  ", true)]
    [InlineData("\\\\server\\share\\a.JPG", true)]
    [InlineData("a.png", false)]               // not a full path
    [InlineData("C:\\shots\\a.txt", false)]
    [InlineData("C:\\a.png\nC:\\b.png", false)]
    [InlineData("see C:\\a.png", false)]
    public void ImagePathIn_TakesOneFullPathToAnImage(string text, bool isPath)
    {
        var path = ClipboardFormatPicker.ImagePathIn(text);

        Assert.Equal(isPath, path != null);
        if (isPath) Assert.False(path!.Contains('"'));
    }
}
