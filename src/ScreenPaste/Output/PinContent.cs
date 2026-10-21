using System.Windows.Media.Imaging;
using ScreenPaste.Settings;

namespace ScreenPaste.Output;

/// <summary>
/// What a pin shows: its image, and the text it was made from when it came from text (the
/// clipboard paste fills that in), so the text can be copied back out. Replaceable: a file
/// dropped on the pin swaps in other content, keeping the pin's turn, zoom and place.
/// </summary>
public sealed record PinContent(BitmapSource Image, string? SourceText = null);

/// <summary>
/// A pin closed recoverably, as <see cref="PinManager.RestoreLastClosed"/> opens it again:
/// what it showed, where its image's top-left was (physical screen px), how it was turned,
/// zoomed and faded, and the settings it follows.
/// </summary>
public sealed record ClosedPin(PinContent Content, int Left, int Top, PinTransformState State, AppSettings Settings);
