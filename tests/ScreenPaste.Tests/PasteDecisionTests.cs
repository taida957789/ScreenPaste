using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The paste hotkey (Snipaste's F3): it pins what the clipboard holds, but while the clipboard
/// is unchanged since the last paste or since a pin was last closed, it brings back the pins
/// closed recently instead. The clipboard's sequence number tells the two apart; 0 means
/// Windows would not say (no clipboard access), and then it always pastes.
/// </summary>
public sealed class PasteDecisionTests
{
    [Fact]
    public void TheFirstPaste_Pastes()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: null, sequenceAtLastClose: null, closedCount: 2));
    }

    [Fact]
    public void ANewClipboard_Pastes_EvenWithClosedPins()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 8, sequenceAtLastPaste: 7, sequenceAtLastClose: 7, closedCount: 2));
    }

    [Fact]
    public void TheSameClipboard_WithAClosedPin_Restores()
    {
        Assert.Equal(PasteChoice.Restore, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: 7, sequenceAtLastClose: null, closedCount: 1));
    }

    [Fact]
    public void TheSameClipboard_WithNothingClosed_PastesAgain()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: 7, sequenceAtLastClose: 7, closedCount: 0));
    }

    [Fact]
    public void AnUnknownSequence_AlwaysPastes()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 0, sequenceAtLastPaste: 0, sequenceAtLastClose: 0, closedCount: 3));
    }

    [Fact]
    public void APinPinnedFromACaptureAndClosed_Restores_WithoutAnyPasteBefore()
    {
        // Ctrl+T in the capture, Esc on the pin, F3: the clipboard (an older copy) is what it
        // was when the pin closed, so the pin comes back rather than that older copy.
        Assert.Equal(PasteChoice.Restore, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: null, sequenceAtLastClose: 7, closedCount: 1));
    }

    [Fact]
    public void ACopyAfterTheClose_Pastes_TheNewClipboard()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 8, sequenceAtLastPaste: null, sequenceAtLastClose: 7, closedCount: 1));
    }

    [Fact]
    public void PressedAgainAfterPastingTheNewClipboard_Restores()
    {
        Assert.Equal(PasteChoice.Restore, PasteDecision.Decide(sequenceNow: 8, sequenceAtLastPaste: 8, sequenceAtLastClose: 7, closedCount: 1));
    }
}
