using System.Windows;
using System.Windows.Input;

namespace ScreenPaste.Output;

/// <summary>What a file dropped on a pin does.</summary>
public enum PinDropAction
{
    /// <summary>No image among the files: nothing changes.</summary>
    Reject,
    /// <summary>The pin shows the image instead.</summary>
    Replace,
    /// <summary>Shift held: a new pin opens with the image, at the drop point.</summary>
    NewPin,
}

/// <summary>
/// Dropping image files on a pin (Snipaste): the first file that loads replaces what the pin
/// shows, or with Shift opens a new pin. Pure, so it is unit tested; <see cref="PinWindow"/>
/// does the loading and the showing.
/// </summary>
public static class PinDrop
{
    public static PinDropAction Decide(ModifierKeys modifiers, bool imageLoaded)
    {
        if (!imageLoaded) return PinDropAction.Reject;
        return modifiers.HasFlag(ModifierKeys.Shift) ? PinDropAction.NewPin : PinDropAction.Replace;
    }

    /// <summary>The keys held as the drop happened. They come with the drop rather than from
    /// <see cref="Keyboard.Modifiers"/>: the files are dragged from another app, which has the
    /// keyboard meanwhile.</summary>
    public static ModifierKeys ModifiersOf(DragDropKeyStates keys)
    {
        var modifiers = ModifierKeys.None;
        if (keys.HasFlag(DragDropKeyStates.ShiftKey)) modifiers |= ModifierKeys.Shift;
        if (keys.HasFlag(DragDropKeyStates.ControlKey)) modifiers |= ModifierKeys.Control;
        if (keys.HasFlag(DragDropKeyStates.AltKey)) modifiers |= ModifierKeys.Alt;
        return modifiers;
    }

    /// <summary>The first of <paramref name="paths"/> that <paramref name="load"/> turns into
    /// an image, trying no more after it; null when none does.</summary>
    public static T? FirstLoadable<T>(IEnumerable<string>? paths, Func<string, T?> load) where T : class
    {
        if (paths == null) return null;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (load(path) is { } loaded) return loaded;
        }
        return null;
    }
}
