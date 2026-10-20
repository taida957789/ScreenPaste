using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Snipaste's close-versus-destroy model: a pin closed with Esc, Ctrl+W or a double-click is
/// remembered so the paste hotkey can bring it back, newest first, up to a capacity (0 turns
/// it off); one destroyed with Shift+Esc never is. The entries here are plain strings; the
/// history never looks inside them.
/// </summary>
public sealed class ClosedPinHistoryTests
{
    private static ClosedPinHistory<string> With(int capacity, params string[] oldestFirst)
    {
        var history = new ClosedPinHistory<string>(capacity);
        foreach (var pin in oldestFirst) history.Add(pin, PinCloseKind.Recoverable);
        return history;
    }

    // ---------------------------------------------------------------- adding ---

    [Fact]
    public void Add_PutsTheNewestFirst()
    {
        var history = new ClosedPinHistory<string>(3);

        history.Add("a", PinCloseKind.Recoverable);
        history.Add("b", PinCloseKind.Recoverable);

        Assert.Equal(new[] { "b", "a" }, history.Items);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Add_PastTheCapacity_DropsTheOldest()
    {
        var history = With(2, "a", "b", "c");

        Assert.Equal(new[] { "c", "b" }, history.Items);
    }

    [Fact]
    public void Add_WithCapacityOne_KeepsOnlyTheLastClosed()
    {
        var history = With(1, "a", "b");

        Assert.Equal(new[] { "b" }, history.Items);
    }

    [Fact]
    public void Add_WithCapacityZero_KeepsNothing()
    {
        var history = new ClosedPinHistory<string>(0);

        bool kept = history.Add("a", PinCloseKind.Recoverable);

        Assert.False(kept);
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void Add_ADestroyedPin_BypassesTheHistory()
    {
        var history = With(3, "a");

        bool kept = history.Add("b", PinCloseKind.Destroy);

        Assert.False(kept);
        Assert.Equal(new[] { "a" }, history.Items);
    }

    [Fact]
    public void Add_ARecoverablePin_SaysItWasKept()
    {
        var history = new ClosedPinHistory<string>(1);

        Assert.True(history.Add("a", PinCloseKind.Recoverable));
    }

    [Fact]
    public void NegativeCapacity_TurnsTheHistoryOff()
    {
        var history = new ClosedPinHistory<string>(-2);

        history.Add("a", PinCloseKind.Recoverable);

        Assert.Equal(0, history.Capacity);
        Assert.Equal(0, history.Count);
    }

    // ---------------------------------------------------------------- restoring ---

    [Fact]
    public void Pop_TakesTheNewestFirst()
    {
        var history = With(3, "a", "b", "c");

        Assert.Equal("c", history.Pop());
        Assert.Equal("b", history.Pop());
        Assert.Equal(new[] { "a" }, history.Items);
    }

    [Fact]
    public void Pop_WhenEmpty_GivesNull()
    {
        var history = new ClosedPinHistory<string>(3);

        Assert.Null(history.Pop());
    }

    [Fact]
    public void Pop_AfterTheLast_GivesNull()
    {
        var history = With(3, "a");

        history.Pop();

        Assert.Null(history.Pop());
    }

    // ---------------------------------------------------------------- capacity at runtime ---

    [Fact]
    public void LoweringTheCapacity_DropsTheOldest()
    {
        var history = With(5, "a", "b", "c", "d");

        history.Capacity = 2;

        Assert.Equal(new[] { "d", "c" }, history.Items);
    }

    [Fact]
    public void LoweringTheCapacityToZero_ForgetsEverything()
    {
        var history = With(5, "a", "b");

        history.Capacity = 0;

        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void RaisingTheCapacity_KeepsWhatIsThere_AndMakesRoom()
    {
        var history = With(1, "a");

        history.Capacity = 3;
        history.Add("b", PinCloseKind.Recoverable);

        Assert.Equal(new[] { "b", "a" }, history.Items);
    }

    // ---------------------------------------------------------------- clearing ---

    [Fact]
    public void Clear_ForgetsEverything_AndKeepsTheCapacity()
    {
        var history = With(3, "a", "b");

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Equal(3, history.Capacity);
    }

    // ---------------------------------------------------------------- the setting ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    [InlineData(5, 5)]
    [InlineData(1000, ClosedPinLimits.MaxPins)]
    public void Capacity_FromTheSetting_IsKeptWithinItsBounds(int setting, int expected)
    {
        Assert.Equal(expected, ClosedPinLimits.Capacity(setting));
    }
}
