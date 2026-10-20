using System.Windows;
using ScreenPaste.Editor;
using ScreenPaste.Native;

namespace ScreenPaste.Capture;

/// <summary>
/// The areas of the last successful snips (R / Shift+R), in physical screen px, newest first.
/// App owns one for its lifetime (never persisted) and hands it to every overlay through the
/// <see cref="CaptureRequest"/>. A cursor walks it: R steps back to older areas and Shift+R
/// the other way, both wrapping around, and each capture starts again from the newest.
/// </summary>
public sealed class SnipRegionHistory
{
    public const int DefaultCapacity = 8;

    private readonly List<Rect> _regions = new();
    private int _position = -1;   // -1: nothing applied yet this capture

    public SnipRegionHistory(int capacity = DefaultCapacity) => Capacity = Math.Max(0, capacity);

    public int Capacity { get; }
    public int Count => _regions.Count;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Rect> Regions => _regions;

    /// <summary>The last successful snip's area, if any.</summary>
    public Rect? Latest => _regions.Count > 0 ? _regions[0] : null;

    /// <summary>Remember a successful snip's area. A repeat of the newest collapses into it;
    /// past the capacity the oldest goes. The cursor starts again from the newest.</summary>
    public void Push(Rect screenRect)
    {
        _position = -1;
        if (screenRect.IsEmpty || screenRect.Width <= 0 || screenRect.Height <= 0) return;
        if (_regions.Count > 0 && _regions[0] == screenRect) return;

        _regions.Insert(0, screenRect);
        if (_regions.Count > Capacity) _regions.RemoveRange(Capacity, _regions.Count - Capacity);
    }

    /// <summary>R: the next older area (the newest the first time), wrapping from the oldest
    /// back to the newest; null when no snip has been made yet.</summary>
    public Rect? Previous()
    {
        if (_regions.Count == 0) return null;
        _position = (_position + 1) % _regions.Count;
        return _regions[_position];
    }

    /// <summary>Shift+R: the next newer area (the oldest the first time), wrapping from the
    /// newest to the oldest; null when no snip has been made yet.</summary>
    public Rect? Next()
    {
        if (_regions.Count == 0) return null;
        _position = _position <= 0 ? _regions.Count - 1 : _position - 1;
        return _regions[_position];
    }

    /// <summary>A new capture: the next R gives the newest area again.</summary>
    public void Reset() => _position = -1;

    public void Clear()
    {
        _regions.Clear();
        _position = -1;
    }

    /// <summary>A remembered area in overlay-local px for <paramref name="screen"/>, kept on it:
    /// an area from a monitor that has since gone is moved back on, keeping its size where it
    /// fits, and one larger than the whole screen shrinks to it.</summary>
    public static Rect FitInto(Rect screenRect, VirtualScreen screen)
    {
        var local = new Rect(screenRect.X - screen.X, screenRect.Y - screen.Y, screenRect.Width, screenRect.Height);
        return HandleGeometry.ClampInto(local, new Rect(0, 0, screen.Width, screen.Height), 1);
    }
}
