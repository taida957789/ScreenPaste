using System.Windows;
using System.Windows.Input;
using ScreenPaste.Output;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Dropping a file on a pin (Snipaste): an image replaces what the pin shows, with Shift it
/// opens a new pin instead, and anything that is not an image is turned away. Of several
/// files the first one that loads is used.
/// </summary>
public sealed class PinDropTests
{
    private const ModifierKeys None = ModifierKeys.None;
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    // ---------------------------------------------------------------- decision ---

    [Theory]
    [InlineData(None, PinDropAction.Replace)]
    [InlineData(Ctrl, PinDropAction.Replace)]
    [InlineData(Alt, PinDropAction.Replace)]
    [InlineData(Shift, PinDropAction.NewPin)]
    [InlineData(Ctrl | Shift, PinDropAction.NewPin)]
    public void AnImage_ReplacesThePin_OrWithShiftOpensANewOne(ModifierKeys modifiers, PinDropAction expected)
    {
        Assert.Equal(expected, PinDrop.Decide(modifiers, imageLoaded: true));
    }

    [Theory]
    [InlineData(None)]
    [InlineData(Shift)]
    public void AFileThatIsNotAnImage_IsTurnedAway(ModifierKeys modifiers)
    {
        Assert.Equal(PinDropAction.Reject, PinDrop.Decide(modifiers, imageLoaded: false));
    }

    // ---------------------------------------------------------------- modifiers ---

    [Theory]
    [InlineData(DragDropKeyStates.None, None)]
    [InlineData(DragDropKeyStates.LeftMouseButton, None)]
    [InlineData(DragDropKeyStates.ShiftKey, Shift)]
    [InlineData(DragDropKeyStates.ShiftKey | DragDropKeyStates.LeftMouseButton, Shift)]
    [InlineData(DragDropKeyStates.ControlKey, Ctrl)]
    [InlineData(DragDropKeyStates.AltKey, Alt)]
    [InlineData(DragDropKeyStates.ControlKey | DragDropKeyStates.ShiftKey, Ctrl | Shift)]
    public void ModifiersOf_ReadsTheKeysHeldDuringTheDrop(DragDropKeyStates keys, ModifierKeys expected)
    {
        Assert.Equal(expected, PinDrop.ModifiersOf(keys));
    }

    // ---------------------------------------------------------------- which file ---

    [Fact]
    public void FirstLoadable_SkipsFilesThatDoNotLoad()
    {
        var loaded = PinDrop.FirstLoadable(new[] { "notes.txt", "shot.png", "other.png" }, Load);

        Assert.Equal("image:shot.png", loaded);
    }

    [Fact]
    public void FirstLoadable_StopsAtTheFirstImage()
    {
        var tried = new List<string>();

        PinDrop.FirstLoadable(new[] { "a.png", "b.png" }, path => { tried.Add(path); return Load(path); });

        Assert.Equal(new[] { "a.png" }, tried);
    }

    [Fact]
    public void FirstLoadable_WithNoImageAmongTheFiles_GivesNull()
    {
        Assert.Null(PinDrop.FirstLoadable(new[] { "a.txt", "b.doc" }, Load));
    }

    [Fact]
    public void FirstLoadable_WithNoFiles_GivesNull()
    {
        Assert.Null(PinDrop.FirstLoadable(Array.Empty<string>(), Load));
        Assert.Null(PinDrop.FirstLoadable(null, Load));
    }

    [Fact]
    public void FirstLoadable_SkipsBlankPaths()
    {
        var tried = new List<string>();

        var loaded = PinDrop.FirstLoadable(new[] { "", "  ", "a.png" }, path => { tried.Add(path); return Load(path); });

        Assert.Equal("image:a.png", loaded);
        Assert.Equal(new[] { "a.png" }, tried);
    }

    /// <summary>A stand-in for ImageLoader.TryLoad: only .png files "load".</summary>
    private static string? Load(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image:" + path : null;
}
