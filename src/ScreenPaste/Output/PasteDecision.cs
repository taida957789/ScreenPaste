namespace ScreenPaste.Output;

/// <summary>What the paste hotkey does this time.</summary>
public enum PasteChoice
{
    /// <summary>Pin what the clipboard holds.</summary>
    Paste,
    /// <summary>Bring back the pin closed last (PinManager.RestoreLastClosed).</summary>
    Restore,
}

/// <summary>
/// The paste hotkey (Snipaste's F3): pin the clipboard, or, pressed again while the clipboard
/// is what it was at the last paste, bring back the pins closed recently instead. Windows'
/// clipboard sequence number tells a new clipboard from the old one. Pure, so it is unit tested.
/// </summary>
public static class PasteDecision
{
    /// <param name="sequenceNow">GetClipboardSequenceNumber now; 0 when Windows would not say.</param>
    /// <param name="sequenceAtLastPaste">The number at the last paste; null before the first.</param>
    /// <param name="closedCount">How many closed pins can still be brought back.</param>
    public static PasteChoice Decide(uint sequenceNow, uint? sequenceAtLastPaste, int closedCount)
    {
        bool unchanged = sequenceNow != 0 && sequenceAtLastPaste == sequenceNow;
        return unchanged && closedCount > 0 ? PasteChoice.Restore : PasteChoice.Paste;
    }
}
