using System.Globalization;

namespace ScreenPaste.Editor;

/// <summary>
/// What Enter, a double-click or a middle-click inside the chosen area does (Snipaste lets
/// the user choose each one). Stored in settings.json by name: EnterAction,
/// DoubleClickAction, MiddleClickAction.
/// </summary>
public enum OutputAction
{
    /// <summary>Nothing: the key or click is turned off.</summary>
    None,
    Copy,
    Pin,
    /// <summary>Ask where to save.</summary>
    Save,
    /// <summary>Save into the default folder without asking.</summary>
    QuickSave,
    CopyAndPin,
}

public static class OutputActions
{
    /// <summary>Every action, in the order the Settings window lists them.</summary>
    public static readonly IReadOnlyList<OutputAction> All = new[]
    {
        OutputAction.None, OutputAction.Copy, OutputAction.Pin,
        OutputAction.Save, OutputAction.QuickSave, OutputAction.CopyAndPin,
    };

    /// <summary>Read a settings value by name, ignoring case and blanks. Empty or unknown
    /// text (a hand edit, a newer version's value) gives <paramref name="fallback"/>.</summary>
    public static OutputAction Parse(string? text, OutputAction fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        // Matched by name, not Enum.TryParse: that would also take "2" or "Copy,Pin".
        var name = text.Trim();
        foreach (var action in All)
            if (action.ToToken().Equals(name, StringComparison.OrdinalIgnoreCase)) return action;
        return fallback;
    }

    /// <summary>The settings value <see cref="Parse"/> reads back.</summary>
    public static string ToToken(this OutputAction action) => action.ToString();

    /// <summary>The area's size as V copies it: "800x600".</summary>
    public static string SizeText(int width, int height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width}x{height}");
}
