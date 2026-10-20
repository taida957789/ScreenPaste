using ScreenPaste.Output;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// What a double-click on a pin does (AppSettings.PinDoubleClickAction): Snipaste closes the
/// pin, which can then be brought back; it can also be turned off. Read by name, so a hand
/// edit or a newer version's value falls back to closing.
/// </summary>
public sealed class PinDoubleClickActionTests
{
    [Theory]
    [InlineData("Close", PinDoubleClickAction.Close)]
    [InlineData("close", PinDoubleClickAction.Close)]
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
    [InlineData(PinDoubleClickAction.None)]
    public void ToToken_ReadsBackAsTheSameAction(PinDoubleClickAction action)
    {
        Assert.Equal(action, PinDoubleClickActions.Parse(action.ToToken()));
    }

    [Fact]
    public void ByDefault_ADoubleClickClosesThePin_AndTheTipIsStillToCome()
    {
        var settings = new AppSettings();

        Assert.Equal(PinDoubleClickAction.Close, PinDoubleClickActions.Parse(settings.PinDoubleClickAction));
        Assert.False(settings.PinDoubleClickTipShown);
    }

    [Fact]
    public void ByDefault_OneClosedPinCanBeBroughtBack_AndEscElsewhereClosesAllPins()
    {
        var settings = new AppSettings();

        Assert.Equal(1, settings.MaxRecoverableClosedPins);
        Assert.True(settings.PinGlobalEscClosesAll);
    }
}
