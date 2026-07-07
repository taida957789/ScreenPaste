using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ScreenPaste;

/// <summary>Generates the system-tray icon at runtime (no binary .ico asset needed).</summary>
internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);

            using var back = new SolidBrush(Color.FromArgb(0x3D, 0xA9, 0xFC));
            g.FillEllipse(back, 1, 1, 30, 30);

            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("S", font, Brushes.White, new RectangleF(0, 0, 32, 32), fmt);
        }

        // GetHicon returns a handle owned by us; the tray keeps the Icon for the app's lifetime.
        return Icon.FromHandle(bmp.GetHicon());
    }
}
