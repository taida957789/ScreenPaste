using System.Windows.Input;
using ScreenPaste.Output;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// What a double-click on a pin does (AppSettings.PinDoubleClickAction): Snipaste closes the
/// pin, which can then be brought back; it can instead toggle the thumbnail, or be turned
/// off. Read by name, so a hand edit or a newer version's value falls back to closing.
/// Shift+double-click always toggles the thumbnail, and with any other modifier the clicks
/// are not a double-click at all.
/// </summary>
public sealed class PinDoubleClickActionTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    [Theory]
    [InlineData("Close", PinDoubleClickAction.Close)]
    [InlineData("close", PinDoubleClickAction.Close)]
    [InlineData("Thumbnail", PinDoubleClickAction.Thumbnail)]
    [InlineData(" thumbnail ", PinDoubleClickAction.Thumbnail)]
    [InlineData("None", PinDoubleClickAction.None)]
    [InlineData(" NONE ", PinDoubleClickAction.None)]
    public void Parse_ReadsTheSettingByName(string text, PinDoubleClickAction expected)
    {
        Assert.Equal(expected, PinDoubleClickActions.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Explode")]
    [InlineData("1")]
    [InlineData("Close,None")]
    public void Parse_EmptyOrUnknownText_Closes(string? text)
    {
        Assert.Equal(PinDoubleClickAction.Close, PinDoubleClickActions.Parse(text));
    }

    [Theory]
    [InlineData(PinDoubleClickAction.Close)]
    [InlineData(PinDoubleClickAction.Thumbnail)]
    [InlineData(PinDoubleClickAction.None)]
    public void ToToken_ReadsBackAsTheSameAction(PinDoubleClickAction action)
    {
        Assert.Equal(action, PinDoubleClickActions.Parse(action.ToToken()));
    }

    [Fact]
    public void All_OffersEveryAction_CloseFirst()
    {
        Assert.Equal(
            new[] { PinDoubleClickAction.Close, PinDoubleClickAction.Thumbnail, PinDoubleClickAction.None },
            PinDoubleClickActions.All);
    }

    [Theory]
    [InlineData(PinDoubleClickAction.Close)]
    [InlineData(PinDoubleClickAction.Thumbnail)]
    [InlineData(PinDoubleClickAction.None)]
    public void APlainDoubleClick_DoesWhatTheSettingSays(PinDoubleClickAction setting)
    {
        Assert.Equal(setting, PinDoubleClickActions.ForClick(setting, None));
    }

    [Theory]
    [InlineData(PinDoubleClickAction.Close)]
    [InlineData(PinDoubleClickAction.Thumbnail)]
    [InlineData(PinDoubleClickAction.None)]
    public void AShiftDoubleClick_AlwaysTogglesTheThumbnail(PinDoubleClickAction setting)
    {
        Assert.Equal(PinDoubleClickAction.Thumbnail, PinDoubleClickActions.ForClick(setting, Shift));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(Alt)]
    [InlineData(Ctrl | Shift)]
    [InlineData(Alt | Shift)]
    public void ADoubleClickWithAnyOtherModifier_DoesNothingAsADoubleClick(ModifierKeys modifiers)
    {
        Assert.Equal(PinDoubleClickAction.None, PinDoubleClickActions.ForClick(PinDoubleClickAction.Close, modifiers));
        Assert.Equal(PinDoubleClickAction.None, PinDoubleClickActions.ForClick(PinDoubleClickAction.Thumbnail, modifiers));
    }

    [Fact]
    public void ByDefault_ADoubleClickClosesThePin_AndTheTipIsStillToCome()
    {
        var settings = new AppSettings();

        Assert.Equal(PinDoubleClickAction.Close, PinDoubleClickActions.Parse(settings.PinDoubleClickAction));
        Assert.False(settings.PinDoubleClickTipShown);
    }

    [Fact]
    public void ByDefault_TheWheelZoomsAroundTheCursor()
    {
        Assert.True(new AppSettings().PinZoomAtCursor);
    }

    [Fact]
    public void ByDefault_OneClosedPinCanBeBroughtBack_AndEscElsewhereClosesAllPins()
    {
        var settings = new AppSettings();

        Assert.Equal(1, settings.MaxRecoverableClosedPins);
        Assert.True(settings.PinGlobalEscClosesAll);
    }
}
