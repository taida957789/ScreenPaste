using System.Collections.Immutable;

namespace ScreenPaste.Output;

/// <summary>
/// The pins picked together (Snipaste's Ctrl+click and Ctrl+A on a pin): a move, a fade, a
/// reset, a close or a destroy on one of them then acts on them all. Immutable, every change
/// gives a new selection; items are told apart by identity, in the order they were picked.
/// Generic so it is tested without windows; <see cref="PinManager"/> keeps the pins'.
/// </summary>
public sealed class PinSelection<T> where T : class
{
    private readonly ImmutableList<T> _items;

    public static PinSelection<T> Empty { get; } = new(ImmutableList<T>.Empty);

    private PinSelection(ImmutableList<T> items) => _items = items;

    public int Count => _items.Count;

    public bool IsEmpty => _items.IsEmpty;

    /// <summary>In the order they were picked.</summary>
    public IReadOnlyList<T> Items => _items;

    public bool Contains(T item) => _items.IndexOf(item, ReferenceEqualityComparer.Instance) >= 0;

    /// <summary>Ctrl+click: pick <paramref name="item"/>, or drop it when it is picked already.</summary>
    public PinSelection<T> Toggle(T item) => Contains(item) ? Without(item) : new(_items.Add(item));

    /// <summary>Ctrl+A: pick <paramref name="items"/> too, each once.</summary>
    public PinSelection<T> With(IEnumerable<T> items)
    {
        var next = _items;
        foreach (var item in items)
            if (next.IndexOf(item, ReferenceEqualityComparer.Instance) < 0) next = next.Add(item);
        return next == _items ? this : new(next);
    }

    /// <summary>Drop <paramref name="item"/> (a pin that closed); the same selection when it was not picked.</summary>
    public PinSelection<T> Without(T item) =>
        Contains(item) ? new(_items.Remove(item, ReferenceEqualityComparer.Instance)) : this;

    public PinSelection<T> Clear() => Empty;

    /// <summary>What a command on <paramref name="item"/> acts on: the whole selection when
    /// it is picked, else just itself.</summary>
    public IReadOnlyList<T> GroupFor(T item) => Contains(item) ? _items : new[] { item };
}
