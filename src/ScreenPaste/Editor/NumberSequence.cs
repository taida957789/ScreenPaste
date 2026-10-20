namespace ScreenPaste.Editor;

/// <summary>
/// The number-marker tool's counter: the number the next click places. Each placement moves
/// it on by one, + and - change it by hand (never below <see cref="First"/>), and undoing or
/// redoing a marker moves it back or on again, unless it was changed by hand since, which
/// keeps the number the user chose. Immutable: the window keeps the current value and swaps
/// it for the result.
/// </summary>
public sealed record NumberSequence
{
    public const int First = 1;
    /// <summary>The largest number a marker shows; three digits still fit its circle.</summary>
    public const int Last = 999;

    public static NumberSequence Start { get; } = new(First);

    public NumberSequence(int next) => Next = Math.Clamp(next, First, Last);

    /// <summary>The number the next marker gets.</summary>
    public int Next { get; }

    /// <summary>A marker with <see cref="Next"/> was placed: the one after it is next.</summary>
    public NumberSequence AfterPlacing() => new(Next + 1);

    /// <summary>+ (positive) or - (negative): change the next number by <paramref name="delta"/>.</summary>
    public NumberSequence Adjusted(int delta) =>
        new((int)Math.Clamp((long)Next + delta, First, Last));

    /// <summary>The marker numbered <paramref name="placed"/> was undone: it is next again, if
    /// the counter still follows it.</summary>
    public NumberSequence AfterUndoing(int placed) => Next == placed + 1 ? new(placed) : this;

    /// <summary>The marker numbered <paramref name="placed"/> was redone: the counter moves past
    /// it again, if it still stands at it.</summary>
    public NumberSequence AfterRedoing(int placed) => Next == placed ? new(placed + 1) : this;
}
