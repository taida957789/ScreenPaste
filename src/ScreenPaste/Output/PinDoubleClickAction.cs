using System.Windows.Input;

namespace ScreenPaste.Output;

/// <summary>What a double-click on a pin does. Stored in settings.json by name:
/// PinDoubleClickAction.</summary>
public enum PinDoubleClickAction
{
    /// <summary>Close the pin, recoverably (Snipaste's default).</summary>
    Close,
    /// <summary>Into thumbnail mode, or back out of it, as Shift+double-click does.</summary>
    Thumbnail,
    /// <summary>Nothing: double-clicks only drag.</summary>
    None,
}

public static class PinDoubleClickActions
{
    /// <summary>Every action, in the order the Settings window offers them.</summary>
    public static readonly IReadOnlyList<PinDoubleClickAction> All = new[]
    {
        PinDoubleClickAction.Close, PinDoubleClickAction.Thumbnail, PinDoubleClickAction.None,
    };

    /// <summary>Read a settings value by name, ignoring case and blanks. Empty or unknown
    /// text (a hand edit, a newer version's value) closes, the default.</summary>
    public static PinDoubleClickAction Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return PinDoubleClickAction.Close;
        // Matched by name, not Enum.TryParse: that would also take "1" or "Close,None".
        var name = text.Trim();
        foreach (var action in All)
            if (action.ToToken().Equals(name, StringComparison.OrdinalIgnoreCase)) return action;
        return PinDoubleClickAction.Close;
    }

    /// <summary>The settings value <see cref="Parse"/> reads back.</summary>
    public static string ToToken(this PinDoubleClickAction action) => action.ToString();

    /// <summary>What a double-click with <paramref name="modifiers"/> held does: a plain one
    /// does what the setting says, Shift always toggles the thumbnail, and with any other
    /// modifier it is no double-click (Ctrl picks pins click by click; the rest drag).</summary>
    public static PinDoubleClickAction ForClick(PinDoubleClickAction setting, ModifierKeys modifiers) => modifiers switch
    {
        ModifierKeys.None => setting,
        ModifierKeys.Shift => PinDoubleClickAction.Thumbnail,
        _ => PinDoubleClickAction.None,
    };
}
