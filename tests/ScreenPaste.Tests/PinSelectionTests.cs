using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The pins picked together (Snipaste's Ctrl+click and Ctrl+A on a pin), which then move,
/// fade, reset and close as one. Immutable, and by identity: two pins showing the same thing
/// are still two pins. Generic, so it is tested without windows.
/// </summary>
public sealed class PinSelectionTests
{
    private sealed record Item(string Name);

    private static readonly Item A = new("a");
    private static readonly Item B = new("b");
    private static readonly Item C = new("c");

    [Fact]
    public void Empty_HoldsNothing()
    {
        var selection = PinSelection<Item>.Empty;

        Assert.True(selection.IsEmpty);
        Assert.Equal(0, selection.Count);
        Assert.False(selection.Contains(A));
    }

    [Fact]
    public void Toggle_AddsAnItemNotYetPicked()
    {
        var selection = PinSelection<Item>.Empty.Toggle(A);

        Assert.True(selection.Contains(A));
        Assert.Equal(1, selection.Count);
    }

    [Fact]
    public void Toggle_RemovesAnItemAlreadyPicked()
    {
        var selection = PinSelection<Item>.Empty.Toggle(A).Toggle(B).Toggle(A);

        Assert.False(selection.Contains(A));
        Assert.Equal(new[] { B }, selection.Items);
    }

    [Fact]
    public void Toggle_LeavesTheOriginalAsItWas()
    {
        var before = PinSelection<Item>.Empty.Toggle(A);

        var after = before.Toggle(B);

        Assert.Equal(new[] { A }, before.Items);
        Assert.Equal(new[] { A, B }, after.Items);
    }

    [Fact]
    public void With_AddsEachItemOnce_InTheOrderGiven()
    {
        var selection = PinSelection<Item>.Empty.Toggle(B).With(new[] { A, B, C, A });

        Assert.Equal(new[] { B, A, C }, selection.Items);
    }

    [Fact]
    public void Without_RemovesOnlyThatItem()
    {
        var selection = PinSelection<Item>.Empty.With(new[] { A, B, C });

        Assert.Equal(new[] { A, C }, selection.Without(B).Items);
        Assert.Same(selection, selection.Without(new Item("x")));
    }

    [Fact]
    public void Clear_EmptiesIt()
    {
        var selection = PinSelection<Item>.Empty.With(new[] { A, B });

        Assert.True(selection.Clear().IsEmpty);
    }

    [Fact]
    public void Contains_GoesByIdentity()
    {
        var selection = PinSelection<Item>.Empty.Toggle(A);

        Assert.False(selection.Contains(new Item("a")));
    }

    [Fact]
    public void GroupFor_APickedItem_IsTheWholeSelection()
    {
        var selection = PinSelection<Item>.Empty.With(new[] { A, B });

        Assert.Equal(new[] { A, B }, selection.GroupFor(B));
    }

    [Fact]
    public void GroupFor_AnItemNotPicked_IsJustThatItem()
    {
        var selection = PinSelection<Item>.Empty.With(new[] { A, B });

        Assert.Equal(new[] { C }, selection.GroupFor(C));
    }
}
