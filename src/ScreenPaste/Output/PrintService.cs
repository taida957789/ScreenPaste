using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Output;

/// <summary>
/// Where a capture goes on the printed page. Units are WPF's 1/96 inch, so one capture pixel
/// printed at 100% is one unit. Pure, so it is unit tested; <see cref="PrintService"/> uses it.
/// </summary>
public static class PrintLayout
{
    // Rounding slack when checking a driver-reported area against the page.
    private const double Slack = 0.5;

    /// <summary>The capture's rectangle on the page: shrunk to fit
    /// <paramref name="printableArea"/> with its aspect ratio kept, never enlarged past 100%,
    /// and centred. An empty capture or area places nothing (a zero-size rect).</summary>
    public static Rect FitToPage(Size image, Rect printableArea)
    {
        var centre = new Point(
            printableArea.X + printableArea.Width / 2,
            printableArea.Y + printableArea.Height / 2);
        if (image.Width <= 0 || image.Height <= 0 || printableArea.Width <= 0 || printableArea.Height <= 0)
            return new Rect(centre, new Size(0, 0));

        double scale = Math.Min(1, Math.Min(printableArea.Width / image.Width, printableArea.Height / image.Height));
        double w = image.Width * scale, h = image.Height * scale;
        return new Rect(centre.X - w / 2, centre.Y - h / 2, w, h);
    }

    /// <summary>The page orientation the print dialog starts with: landscape for a capture
    /// wider than it is tall.</summary>
    public static bool IsLandscape(Size image) => image.Width > image.Height;

    /// <summary>
    /// The printable area to lay out in. Some drivers keep reporting the portrait area after
    /// the page has been turned landscape (or the other way round), which would cut the
    /// capture off; an area that only fits the page turned is turned to match. With nothing
    /// usable reported, the whole page.
    /// </summary>
    public static Rect Imageable(Size page, Rect reported)
    {
        var whole = new Rect(page);
        if (reported.IsEmpty || reported.Width <= 0 || reported.Height <= 0) return whole;
        if (page.Width <= 0 || page.Height <= 0 || Fits(reported, page)) return reported;

        var turned = new Rect(reported.Y, reported.X, reported.Height, reported.Width);
        return Fits(turned, page) ? turned : whole;
    }

    private static bool Fits(Rect area, Size page) =>
        area.X >= -Slack && area.Y >= -Slack &&
        area.Right <= page.Width + Slack && area.Bottom <= page.Height + Slack;
}

/// <summary>Prints a capture on one page through the standard Windows print dialog.</summary>
public static class PrintService
{
    /// <summary>
    /// Ask for a printer, then print <paramref name="image"/> fitted to the page
    /// (<see cref="PrintLayout"/>). <paramref name="beforePrinting"/> runs once a printer is
    /// chosen, just before the job goes out: a printer's own dialog (a PDF printer's file
    /// name) can open then, and a topmost window should get out of its way. False when the
    /// user cancels either dialog. A failing printer throws (<see cref="PrintSystemException"/>
    /// or whatever its driver raised), a print dialog that cannot open
    /// <see cref="PrintDialogException"/>; either can come after <paramref name="beforePrinting"/>.
    /// </summary>
    public static bool Print(BitmapSource image, string jobName, Action beforePrinting)
    {
        var size = new Size(image.PixelWidth, image.PixelHeight);
        var dialog = new PrintDialog();
        PresetOrientation(dialog, size);
        if (dialog.ShowDialog() != true) return false;

        var page = new DrawingVisual();
        using (var dc = page.RenderOpen())
            dc.DrawImage(image, PrintLayout.FitToPage(size, ImageableArea(dialog)));

        beforePrinting();
        try
        {
            dialog.PrintVisual(page, jobName);
            return true;
        }
        catch (PrintingCanceledException)
        {
            return false;
        }
    }

    /// <summary>Start the dialog in the capture's orientation; the user can still change it.</summary>
    private static void PresetOrientation(PrintDialog dialog, Size image)
    {
        try
        {
            dialog.PrintTicket.PageOrientation = PrintLayout.IsLandscape(image)
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;
        }
        catch (PrintSystemException)
        {
            // No default printer to ask: the dialog starts with its own choice.
        }
    }

    private static Rect ImageableArea(PrintDialog dialog)
    {
        var page = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        try
        {
            if (dialog.PrintQueue?.GetPrintCapabilities(dialog.PrintTicket).PageImageableArea is { } a)
                return PrintLayout.Imageable(page, new Rect(a.OriginWidth, a.OriginHeight, a.ExtentWidth, a.ExtentHeight));
        }
        catch (PrintSystemException)
        {
            // The driver could not say; lay out on the whole page.
        }
        return new Rect(page);
    }
}
