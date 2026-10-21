using ScreenPaste.Editor;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The number-marker tool's counter: each click places the next number, + and - change the
/// number the next click places (never below 1), and undoing a marker puts the counter back
/// unless it was changed by hand since.
/// </summary>
public sealed class NumberSequenceTests
{
    // ---------------------------------------------------------------- placing ---

    [Fact]
    public void Start_PlacesOneFirst()
    {
        Assert.Equal(1, NumberSequence.Start.Next);
    }

    [Fact]
    public void AfterPlacing_TheNextNumberIsOneHigher()
    {
        var sequence = NumberSequence.Start.AfterPlacing().AfterPlacing();

        Assert.Equal(3, sequence.Next);
    }

    [Fact]
    public void AfterPlacing_LeavesTheEarlierSequenceAlone()
    {
        var before = new NumberSequence(4);

        var after = before.AfterPlacing();

        Assert.Equal(4, before.Next);
        Assert.Equal(5, after.Next);
    }

    // ---------------------------------------------------------------- + and - ---

    [Theory]
    [InlineData(5, 1, 6)]
    [InlineData(5, -1, 4)]
    [InlineData(5, 3, 8)]
    [InlineData(2, -1, 1)]
    public void Adjusted_ChangesTheNextNumber(int next, int delta, int expected)
    {
        Assert.Equal(expected, new NumberSequence(next).Adjusted(delta).Next);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(3, -10)]
    public void Adjusted_NeverGoesBelowOne(int next, int delta)
    {
        Assert.Equal(NumberSequence.First, new NumberSequence(next).Adjusted(delta).Next);
    }

    [Fact]
    public void Adjusted_ThenPlacing_ContinuesFromTheAdjustedNumber()
    {
        var sequence = NumberSequence.Start.Adjusted(4);

        Assert.Equal(5, sequence.Next);
        Assert.Equal(6, sequence.AfterPlacing().Next);
    }

    [Fact]
    public void Adjusted_ByAHugeDelta_StaysInRange()
    {
        var sequence = new NumberSequence(5);

        Assert.Equal(NumberSequence.Last, sequence.Adjusted(int.MaxValue).Next);
        Assert.Equal(NumberSequence.First, sequence.Adjusted(int.MinValue).Next);
    }

    // ---------------------------------------------------------------- range ---

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Constructor_ClampsUpToOne(int next)
    {
        Assert.Equal(NumberSequence.First, new NumberSequence(next).Next);
    }

    [Fact]
    public void Constructor_ClampsDownToTheLargestNumberAMarkerShows()
    {
        Assert.Equal(NumberSequence.Last, new NumberSequence(NumberSequence.Last + 50).Next);
    }

    [Fact]
    public void AfterPlacing_TheLast_StaysAtTheLast()
    {
        Assert.Equal(NumberSequence.Last, new NumberSequence(NumberSequence.Last).AfterPlacing().Next);
    }

    // ---------------------------------------------------------------- undo / redo ---

    [Fact]
    public void AfterUndoing_TheLastMarker_PlacesItsNumberAgain()
    {
        // Arrange: 1, 2 and 3 placed, so 4 is next.
        var sequence = NumberSequence.Start.AfterPlacing().AfterPlacing().AfterPlacing();

        // Act
        var undone = sequence.AfterUndoing(3);

        // Assert
        Assert.Equal(3, undone.Next);
    }

    [Fact]
    public void AfterUndoing_EveryMarker_CountsBackToOne()
    {
        var sequence = NumberSequence.Start.AfterPlacing().AfterPlacing();

        var undone = sequence.AfterUndoing(2).AfterUndoing(1);

        Assert.Equal(1, undone.Next);
    }

    [Fact]
    public void AfterUndoing_KeepsANumberChosenByHandSince()
    {
        // 1 placed, then + pressed up to 5: undoing marker 1 must not throw the 5 away.
        var sequence = NumberSequence.Start.AfterPlacing().Adjusted(3);

        var undone = sequence.AfterUndoing(1);

        Assert.Equal(5, undone.Next);
    }

    [Fact]
    public void AfterRedoing_MovesOnPastTheRestoredMarker()
    {
        var undone = NumberSequence.Start.AfterPlacing().AfterPlacing().AfterUndoing(2).AfterUndoing(1);

        var redone = undone.AfterRedoing(1).AfterRedoing(2);

        Assert.Equal(3, redone.Next);
    }

    [Fact]
    public void AfterRedoing_KeepsANumberChosenByHandSince()
    {
        var sequence = NumberSequence.Start.AfterPlacing().AfterUndoing(1).Adjusted(6);

        var redone = sequence.AfterRedoing(1);

        Assert.Equal(7, redone.Next);
    }

    [Fact]
    public void Sequences_WithTheSameNextNumber_AreEqual()
    {
        Assert.Equal(new NumberSequence(3), NumberSequence.Start.AfterPlacing().AfterPlacing());
    }
}
