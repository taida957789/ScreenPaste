using System.Windows;
using System.Windows.Media.Imaging;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// The snip history that , / . replay: the last successful snips, newest first, in memory
/// only. It holds at most <see cref="Capacity"/> records and <see cref="BudgetBytes"/> bytes
/// between them, dropping the oldest first. A position walks it: -1 is the live screen of the
/// capture in progress, 0 the newest record. Generic so the bookkeeping is tested without
/// bitmaps; the app stores <see cref="SnipRecord"/>s.
/// </summary>
public sealed class SnipRecordStore<T> where T : class
{
    private readonly List<(T Record, long Bytes)> _records = new();

    public SnipRecordStore(int capacity, long budgetBytes)
    {
        Capacity = Math.Max(0, capacity);
        BudgetBytes = Math.Max(0, budgetBytes);
    }

    public int Capacity { get; }
    public long BudgetBytes { get; }
    public int Count => _records.Count;
    public long TotalBytes { get; private set; }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<T> Items => _records.Select(r => r.Record).ToList();

    /// <summary>-1 on the live screen, else the index of the record shown.</summary>
    public int Position { get; private set; } = -1;

    public bool IsLive => Position < 0;

    /// <summary>The record shown, or null on the live screen.</summary>
    public T? Current => IsLive ? null : _records[Position].Record;

    /// <summary>Remember a snip of <paramref name="bytes"/>, dropping the oldest records until
    /// the count and the budget allow it. One larger than the whole budget is turned away and
    /// the others are kept. Either way the position goes back to the live screen.</summary>
    public bool Add(T record, long bytes)
    {
        Position = -1;
        if (!Accepts(bytes)) return false;

        _records.Insert(0, (record, bytes));
        TotalBytes += bytes;
        while (_records.Count > Capacity || TotalBytes > BudgetBytes) RemoveAt(_records.Count - 1);
        return true;
    }

    /// <summary>Whether <see cref="Add"/> would keep a record of <paramref name="bytes"/>
    /// (dropping older ones to make room), so a caller can skip building one that would be
    /// turned away: none while the history is off, nor one larger than the whole budget.</summary>
    public bool Accepts(long bytes) => Capacity > 0 && bytes >= 0 && bytes <= BudgetBytes;

    /// <summary>',': show the next older record (the newest, from the live screen). False at
    /// the oldest, or with no records at all.</summary>
    public bool MoveOlder()
    {
        if (Position + 1 >= _records.Count) return false;
        Position++;
        return true;
    }

    /// <summary>'.': show the next newer record; past the newest, the live screen again.
    /// False when already on the live screen.</summary>
    public bool MoveNewer()
    {
        if (IsLive) return false;
        Position--;
        return true;
    }

    /// <summary>A new capture starts on its own live screen.</summary>
    public void Reset() => Position = -1;

    public void Clear()
    {
        _records.Clear();
        TotalBytes = 0;
        Position = -1;
    }

    /// <summary>Drop every record <paramref name="match"/> picks (one of another screen layout,
    /// which the overlay cannot show); the position goes back to the live screen.</summary>
    public int RemoveAll(Func<T, bool> match)
    {
        int removed = 0;
        for (int i = _records.Count - 1; i >= 0; i--)
        {
            if (!match(_records[i].Record)) continue;
            RemoveAt(i);
            removed++;
        }
        Position = -1;
        return removed;
    }

    private void RemoveAt(int index)
    {
        TotalBytes -= _records[index].Bytes;
        _records.RemoveAt(index);
    }
}

/// <summary>
/// One successful snip, as , / . bring it back: the screenshot it was cut from, the screen
/// layout and area, the area flattened with its annotations (and without the pointer, which
/// <see cref="Cursor"/> keeps apart), all frozen.
/// </summary>
public sealed record SnipRecord(
    BitmapSource Screenshot, VirtualScreen Screen, Int32Rect Region, BitmapSource Annotated, CursorSnapshot? Cursor)
{
    /// <summary>What it holds in memory: 32 bits a pixel, for both bitmaps.</summary>
    public long Bytes =>
        BytesOf(Screenshot.PixelWidth, Screenshot.PixelHeight) + BytesOf(Annotated.PixelWidth, Annotated.PixelHeight);

    public static long BytesOf(int width, int height) => (long)Math.Max(0, width) * Math.Max(0, height) * 4;
}

/// <summary>The snip history's bounds: the settings may lower Snipaste's 8 records and the
/// 512 MB budget, but never raise them.</summary>
public static class SnipHistoryLimits
{
    public const int MaxRecords = 8;
    public const int MaxBudgetMb = 512;
    private const long BytesPerMb = 1024 * 1024;

    /// <summary>AppSettings.SnipHistoryCount, within 0 (history off) to 8.</summary>
    public static int Capacity(int setting) => Math.Clamp(setting, 0, MaxRecords);

    /// <summary>AppSettings.SnipHistoryBudgetMb in bytes, within 0 to 512 MB.</summary>
    public static long BudgetBytes(int megabytes) => Math.Clamp(megabytes, 0, MaxBudgetMb) * BytesPerMb;
}
