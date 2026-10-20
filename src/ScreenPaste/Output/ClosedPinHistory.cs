namespace ScreenPaste.Output;

/// <summary>How a pin goes away (Snipaste's close-versus-destroy model).</summary>
public enum PinCloseKind
{
    /// <summary>Esc, Ctrl+W, a double-click, the menu's Close: the paste hotkey can bring it back.</summary>
    Recoverable,
    /// <summary>Shift+Esc or the menu's Destroy: gone for good.</summary>
    Destroy,
}

/// <summary>
/// The pins closed recoverably, newest first, so the paste hotkey can bring the last one
/// back. It holds at most <see cref="Capacity"/> of them (0 turns it off) and drops the
/// oldest first; a destroyed pin is never kept. In memory only. Generic so the bookkeeping is
/// tested without windows or bitmaps; <see cref="PinManager"/> keeps <see cref="ClosedPin"/>s.
/// </summary>
public sealed class ClosedPinHistory<T> where T : class
{
    private readonly List<T> _items = new();   // newest first
    private int _capacity;

    public ClosedPinHistory(int capacity) => Capacity = capacity;

    /// <summary>How many closed pins are kept. Lowering it drops the oldest at once.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(0, value);
            Trim();
        }
    }

    public int Count => _items.Count;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<T> Items => _items.ToList();

    /// <summary>Remember a pin closed as <paramref name="kind"/>; the oldest goes when the
    /// history is full. False when it was not kept: destroyed, or the history is off.</summary>
    public bool Add(T item, PinCloseKind kind)
    {
        if (kind == PinCloseKind.Destroy || _capacity == 0) return false;
        _items.Insert(0, item);
        Trim();
        return true;
    }

    /// <summary>Take the newest closed pin out, to open it again; null when there is none.</summary>
    public T? Pop()
    {
        if (_items.Count == 0) return null;
        var newest = _items[0];
        _items.RemoveAt(0);
        return newest;
    }

    public void Clear() => _items.Clear();

    private void Trim()
    {
        if (_items.Count > _capacity) _items.RemoveRange(_capacity, _items.Count - _capacity);
    }
}

/// <summary>The closed-pin history's bounds: each kept pin holds its whole image, so the
/// setting is capped.</summary>
public static class ClosedPinLimits
{
    public const int MaxPins = 16;

    /// <summary>AppSettings.MaxRecoverableClosedPins, within 0 (history off) to <see cref="MaxPins"/>.</summary>
    public static int Capacity(int setting) => Math.Clamp(setting, 0, MaxPins);
}
