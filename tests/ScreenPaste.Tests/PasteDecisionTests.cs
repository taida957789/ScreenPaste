using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The paste hotkey (Snipaste's F3): it pins what the clipboard holds, and pressed again with
/// the clipboard unchanged since the last paste it brings back the pins closed recently
/// instead. The clipboard's sequence number tells the two apart; 0 means Windows would not
/// say (no clipboard access), and then it always pastes.
/// </summary>
public sealed class PasteDecisionTests
{
    [Fact]
    public void TheFirstPaste_Pastes()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: null, closedCount: 2));
    }

    [Fact]
    public void ANewClipboard_Pastes_EvenWithClosedPins()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 8, sequenceAtLastPaste: 7, closedCount: 2));
    }

    [Fact]
    public void TheSameClipboard_WithAClosedPin_Restores()
    {
        Assert.Equal(PasteChoice.Restore, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: 7, closedCount: 1));
    }

    [Fact]
    public void TheSameClipboard_WithNothingClosed_PastesAgain()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 7, sequenceAtLastPaste: 7, closedCount: 0));
    }

    [Fact]
    public void AnUnknownSequence_AlwaysPastes()
    {
        Assert.Equal(PasteChoice.Paste, PasteDecision.Decide(sequenceNow: 0, sequenceAtLastPaste: 0, closedCount: 3));
    }
}
