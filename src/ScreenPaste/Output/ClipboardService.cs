using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenPaste.Output;

public static class ClipboardService
{
    /// <summary>Copy an image to the clipboard, retrying briefly if it is locked.</summary>
    public static bool CopyImage(BitmapSource image)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return true;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(40);
            }
        }
        return false;
    }
}
