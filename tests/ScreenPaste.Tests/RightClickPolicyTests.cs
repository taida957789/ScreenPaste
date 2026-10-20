using ScreenPaste.Capture;
using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The capture overlay's one right-click rule, Snipaste-style: each click undoes the
/// innermost thing in progress. Framing: cancel the drag, else leave. Editing: end the
/// text, deselect, drop the tool, then discard the area and frame again (asking first
/// when annotations would be lost). A fill tool marks areas instead of finishing.
/// </summary>
public sealed class RightClickPolicyTests
{
    private static RightClickContext Selecting() => new() { Phase = OverlayPhase.Selecting };

    private static RightClickContext Editing() => new()
    {
        Phase = OverlayPhase.Editing,
        InsideRegion = true,
        Tool = ToolKind.None,
    };

    // ---------------------------------------------------------------- selecting ---

    [Fact]
    public void Selecting_WithNoAreaChosen_AbortsTheCapture()
    {
        Assert.Equal(RightClickAction.Abort, RightClickPolicy.Decide(Selecting()));
    }

    [Fact]
    public void Selecting_MidDrag_CancelsTheDragOnly()
    {
        var ctx = Selecting() with { Dragging = true };

        Assert.Equal(RightClickAction.CancelDrag, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Selecting_IgnoresEditingState()
    {
        // Leftovers from an earlier editing pass never keep the overlay open.
        var ctx = Selecting() with { HasSelection = true, HasAnnotations = true, Tool = ToolKind.Shape };

        Assert.Equal(RightClickAction.Abort, RightClickPolicy.Decide(ctx));
    }

    // ---------------------------------------------------------------- editing ---

    [Fact]
    public void Editing_MidDrag_IsLeftToTheDrag()
    {
        // Drawing, moving or resizing owns the held left button; a right-click must not
        // tear the region down underneath it.
        var ctx = Editing() with { Dragging = true, TextActive = true, HasAnnotations = true };

        Assert.Equal(RightClickAction.None, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_WhileTyping_CommitsTheTextFirst()
    {
        var ctx = Editing() with
        {
            TextActive = true, HasSelection = true, Tool = ToolKind.Text, HasAnnotations = true,
        };

        Assert.Equal(RightClickAction.CommitText, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_WhileTyping_CommitsBeforeAFillToolMarks()
    {
        var ctx = Editing() with { TextActive = true, Tool = ToolKind.Shape, InsideRegion = false };

        Assert.Equal(RightClickAction.CommitText, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_WithASelection_Deselects()
    {
        var ctx = Editing() with { HasSelection = true, HasAnnotations = true };

        Assert.Equal(RightClickAction.Deselect, RightClickPolicy.Decide(ctx));
    }

    [Theory]
    [InlineData(ToolKind.MarkerPen)]
    [InlineData(ToolKind.Text)]
    [InlineData(ToolKind.Line)]
    public void Editing_WithASelection_DeselectsBeforeDroppingTheTool(ToolKind tool)
    {
        var ctx = Editing() with { HasSelection = true, Tool = tool };

        Assert.Equal(RightClickAction.Deselect, RightClickPolicy.Decide(ctx));
    }

    [Theory]
    [InlineData(ToolKind.MarkerPen)]
    [InlineData(ToolKind.Highlighter)]
    [InlineData(ToolKind.Text)]
    [InlineData(ToolKind.Line)]
    [InlineData(ToolKind.Sticker)]
    [InlineData(ToolKind.Magnify)]
    public void Editing_WithAToolActive_DropsBackToMoveMode(ToolKind tool)
    {
        var ctx = Editing() with { Tool = tool, HasAnnotations = true, AutoDetectAvailable = true };

        Assert.Equal(RightClickAction.DropTool, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_WithNothingLeftToFinish_ReturnsToSelecting()
    {
        Assert.Equal(RightClickAction.ReturnToSelecting, RightClickPolicy.Decide(Editing()));
    }

    [Fact]
    public void Editing_WithAnnotations_AsksBeforeReturningToSelecting()
    {
        var ctx = Editing() with { HasAnnotations = true };

        Assert.Equal(RightClickAction.ConfirmThenReturn, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_OutsideTheRegion_WithNoTool_StillReturnsToSelecting()
    {
        var ctx = Editing() with { InsideRegion = false };

        Assert.Equal(RightClickAction.ReturnToSelecting, RightClickPolicy.Decide(ctx));
    }

    // ---------------------------------------------------------------- fill tools ---

    [Theory]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.Blur)]
    public void Editing_AFillToolOutsideTheRegion_MarksTheWholeArea(ToolKind tool)
    {
        var ctx = Editing() with { Tool = tool, InsideRegion = false, HasSelection = true };

        Assert.Equal(RightClickAction.MarkWholeArea, RightClickPolicy.Decide(ctx));
    }

    [Theory]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.Blur)]
    public void Editing_AFillToolInsideTheRegion_MarksTheElementUnderTheCursor(ToolKind tool)
    {
        var ctx = Editing() with { Tool = tool, AutoDetectAvailable = true, HasSelection = true };

        Assert.Equal(RightClickAction.AutoMarkElement, RightClickPolicy.Decide(ctx));
    }

    [Theory]
    [InlineData(ToolKind.Shape)]
    [InlineData(ToolKind.Blur)]
    public void Editing_AFillToolWithoutAutoDetect_FinishesLikeAnyOtherTool(ToolKind tool)
    {
        var ctx = Editing() with { Tool = tool };

        Assert.Equal(RightClickAction.DropTool, RightClickPolicy.Decide(ctx));
    }

    [Fact]
    public void Editing_AnOtherToolOutsideTheRegion_DoesNotMark()
    {
        var ctx = Editing() with { Tool = ToolKind.MarkerPen, InsideRegion = false, AutoDetectAvailable = true };

        Assert.Equal(RightClickAction.DropTool, RightClickPolicy.Decide(ctx));
    }

    [Theory]
    [InlineData(ToolKind.Shape, true)]
    [InlineData(ToolKind.Blur, true)]
    [InlineData(ToolKind.None, false)]
    [InlineData(ToolKind.MarkerPen, false)]
    [InlineData(ToolKind.Line, false)]
    [InlineData(ToolKind.Magnify, false)]
    public void MarksAreas_IsTrueForTheFillTools(ToolKind tool, bool expected)
    {
        Assert.Equal(expected, RightClickPolicy.MarksAreas(tool));
    }
}
