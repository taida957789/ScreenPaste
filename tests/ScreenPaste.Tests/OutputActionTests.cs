using ScreenPaste.Editor;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// What Enter, a double-click and a middle-click inside the chosen area do. The choice is a
/// settings.json string, so reading it back has to survive hand edits: any casing, stray
/// blanks, and values this version does not know.
/// </summary>
public sealed class OutputActionTests
{
    public static TheoryData<OutputAction> AllActions()
    {
        var data = new TheoryData<OutputAction>();
        foreach (var action in Enum.GetValues<OutputAction>()) data.Add(action);
        return data;
    }

    // ---------------------------------------------------------------- parse ---

    [Theory]
    [InlineData("Copy", OutputAction.Copy)]
    [InlineData("copy", OutputAction.Copy)]
    [InlineData("PIN", OutputAction.Pin)]
    [InlineData("Save", OutputAction.Save)]
    [InlineData("quicksave", OutputAction.QuickSave)]
    [InlineData("CopyAndPin", OutputAction.CopyAndPin)]
    [InlineData("  Pin  ", OutputAction.Pin)]
    public void Parse_ReadsEveryAction_IgnoringCaseAndBlanks(string text, OutputAction expected)
    {
        Assert.Equal(expected, OutputActions.Parse(text, OutputAction.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Print")]       // not an action this setting offers
    [InlineData("2")]           // Enum.TryParse would take a number
    [InlineData("Copy,Pin")]    // ... or a flags list
    public void Parse_EmptyOrUnknownText_FallsBackToTheDefault(string? text)
    {
        Assert.Equal(OutputAction.Pin, OutputActions.Parse(text, OutputAction.Pin));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    public void Parse_None_TurnsTheActionOff_WhateverTheDefault(string text)
    {
        Assert.Equal(OutputAction.None, OutputActions.Parse(text, OutputAction.Copy));
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public void ToToken_RoundTripsThroughParse(OutputAction action)
    {
        // Arrange: a default that differs, so a failed parse cannot pass by accident.
        var fallback = action == OutputAction.Copy ? OutputAction.Pin : OutputAction.Copy;

        // Act
        var token = action.ToToken();

        // Assert
        Assert.Equal(action.ToString(), token);
        Assert.Equal(action, OutputActions.Parse(token, fallback));
    }

    [Fact]
    public void All_ListsEveryActionOnce_NoneFirst()
    {
        Assert.Equal(Enum.GetValues<OutputAction>().Length, OutputActions.All.Count);
        Assert.Equal(Enum.GetValues<OutputAction>().Length, OutputActions.All.Distinct().Count());
        Assert.Equal(OutputAction.None, OutputActions.All[0]);
    }

    // ---------------------------------------------------------------- defaults ---

    [Fact]
    public void Defaults_FollowSnipaste_EnterAndDoubleClickCopy_MiddleClickPins()
    {
        var settings = new AppSettings();

        Assert.Equal(OutputAction.Copy, OutputActions.Parse(settings.EnterAction, OutputAction.None));
        Assert.Equal(OutputAction.Copy, OutputActions.Parse(settings.DoubleClickAction, OutputAction.None));
        Assert.Equal(OutputAction.Pin, OutputActions.Parse(settings.MiddleClickAction, OutputAction.None));
    }

    // ---------------------------------------------------------------- size text ---

    [Theory]
    [InlineData(800, 600, "800x600")]
    [InlineData(1, 1, "1x1")]
    [InlineData(3840, 2160, "3840x2160")]
    public void SizeText_IsWidthByHeight_WithNoSpacesOrSeparators(int width, int height, string expected)
    {
        Assert.Equal(expected, OutputActions.SizeText(width, height));
    }
}
