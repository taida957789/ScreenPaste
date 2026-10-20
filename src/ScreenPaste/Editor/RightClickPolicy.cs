using ScreenPaste.Capture;

namespace ScreenPaste.Editor;

/// <summary>What a right-click in the capture overlay does.</summary>
public enum RightClickAction
{
    /// <summary>Nothing: the click is left alone.</summary>
    None,
    /// <summary>Framing: drop the rubber band and go back to hovering.</summary>
    CancelDrag,
    /// <summary>Finish the text being typed.</summary>
    CommitText,
    /// <summary>Clear the annotation selection.</summary>
    Deselect,
    /// <summary>Finish the tool: back to move mode (no tool).</summary>
    DropTool,
    /// <summary>A fill tool marks the UI element under the cursor.</summary>
    AutoMarkElement,
    /// <summary>A fill tool, clicked outside the region, marks all of it.</summary>
    MarkWholeArea,
    /// <summary>Discard the region and frame a new one.</summary>
    ReturnToSelecting,
    /// <summary>The same, asking first: annotations would be lost.</summary>
    ConfirmThenReturn,
    /// <summary>Leave the capture.</summary>
    Abort,
}

/// <summary>Everything about the overlay's state that decides what a right-click does.</summary>
public readonly record struct RightClickContext
{
    public OverlayPhase Phase { get; init; }
    /// <summary>The left button is held: framing, drawing, moving or resizing.</summary>
    public bool Dragging { get; init; }
    /// <summary>A text annotation is being typed.</summary>
    public bool TextActive { get; init; }
    /// <summary>An annotation is selected.</summary>
    public bool HasSelection { get; init; }
    public ToolKind Tool { get; init; }
    /// <summary>The region holds at least one annotation (ink included).</summary>
    public bool HasAnnotations { get; init; }
    /// <summary>The click landed inside the chosen region.</summary>
    public bool InsideRegion { get; init; }
    /// <summary>UI element detection can find the element under the cursor.</summary>
    public bool AutoDetectAvailable { get; init; }
}

/// <summary>
/// The capture overlay's one right-click rule, Snipaste-style: each click undoes the
/// innermost thing in progress. Framing, it cancels the drag, else leaves. Editing, it ends
/// the text, lets a fill tool mark an area, deselects, drops the tool, and finally discards
/// the region to frame again (asking first when annotations would be lost), so the next
/// right-click leaves. Pure, so the precedence is unit tested; the window executes it.
/// </summary>
public static class RightClickPolicy
{
    public static RightClickAction Decide(RightClickContext ctx)
    {
        if (ctx.Phase == OverlayPhase.Selecting)
            return ctx.Dragging ? RightClickAction.CancelDrag : RightClickAction.Abort;

        // The held left button owns the gesture; tearing the region down under it would
        // leave the drag working on a phase that is gone.
        if (ctx.Dragging) return RightClickAction.None;
        if (ctx.TextActive) return RightClickAction.CommitText;
        if (MarksAreas(ctx.Tool))
        {
            if (!ctx.InsideRegion) return RightClickAction.MarkWholeArea;
            if (ctx.AutoDetectAvailable) return RightClickAction.AutoMarkElement;
        }
        if (ctx.HasSelection) return RightClickAction.Deselect;
        if (ctx.Tool != ToolKind.None) return RightClickAction.DropTool;
        return ctx.HasAnnotations ? RightClickAction.ConfirmThenReturn : RightClickAction.ReturnToSelecting;
    }

    /// <summary>The fill-type tools, which a right-click turns into a whole-area or
    /// element mark rather than finishing.</summary>
    public static bool MarksAreas(ToolKind tool) => tool is ToolKind.Shape or ToolKind.Blur;
}
