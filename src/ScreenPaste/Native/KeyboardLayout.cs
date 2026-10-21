using System.Windows.Input;

namespace ScreenPaste.Native;

/// <summary>What keys type on the current keyboard layout, for the character shortcuts
/// (+, -, [, ...) whose key differs from layout to layout.</summary>
public static class KeyboardLayout
{
    private const uint DeadKeyFlag = 0x80000000;
    private const int ShiftOnly = 1;   // VkKeyScan's shift state for Shift and nothing else

    /// <summary>What <paramref name="key"/> types with no modifier held, or null: none, or a
    /// dead key waiting for its letter.</summary>
    public static string? UnshiftedText(Key key) => UnshiftedText(KeyInterop.VirtualKeyFromKey(key));

    /// <summary>The key that types <paramref name="c"/> with Shift alone, as that key types
    /// unshifted ("1" for "!", "[" for "{" on a US layout); null when no key types it, or it
    /// takes no modifier or another one (AltGr).</summary>
    public static string? ShiftedKeyOf(char c)
    {
        short scan = NativeMethods.VkKeyScan(c);
        if (scan == -1 || ((scan >> 8) & 0xFF) != ShiftOnly) return null;
        return UnshiftedText(scan & 0xFF);
    }

    private static string? UnshiftedText(int vk)
    {
        if (vk == 0) return null;
        uint mapped = NativeMethods.MapVirtualKey((uint)vk, NativeMethods.MAPVK_VK_TO_CHAR);
        if (mapped == 0 || (mapped & DeadKeyFlag) != 0) return null;
        return ((char)(mapped & 0xFFFF)).ToString();
    }
}
