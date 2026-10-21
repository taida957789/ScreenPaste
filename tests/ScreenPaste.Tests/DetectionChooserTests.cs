using System.Windows;
using ScreenPaste.Capture;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Which detected rect the selection phase highlights. The chain under the cursor runs
/// from the deepest UI element (index 0) out to the window itself (last entry); Tab picks
/// between element and window detection, and the wheel walks the levels.
/// </summary>
public sealed class DetectionChooserTests
{
    private static readonly Rect Button = new(110, 110, 40, 20);
    private static readonly Rect Panel = new(100, 100, 300, 200);
    private static readonly Rect Window = new(50, 50, 800, 600);
    private static readonly Rect[] Chain = { Button, Panel, Window };

    // ---------------------------------------------------------------- Pick ---

    [Fact]
    public void Pick_InWindowMode_ReturnsTheWindow_WhateverTheLevel()
    {
        Assert.Equal(Window, DetectionChooser.Pick(Chain, DetectMode.Window, 0));
        Assert.Equal(Window, DetectionChooser.Pick(Chain, DetectMode.Window, 1));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Pick_InElementMode_ReturnsTheEntryAtTheLevel(int level, int expectedIndex)
    {
        Assert.Equal(Chain[expectedIndex], DetectionChooser.Pick(Chain, DetectMode.Element, level));
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(3, 2)]
    [InlineData(99, 2)]
    public void Pick_InElementMode_ClampsTheLevelIntoTheChain(int level, int expectedIndex)
    {
        Assert.Equal(Chain[expectedIndex], DetectionChooser.Pick(Chain, DetectMode.Element, level));
    }

    [Theory]
    [InlineData(DetectMode.Element)]
    [InlineData(DetectMode.Window)]
    public void Pick_FromAnEmptyChain_ReturnsNothing(DetectMode mode)
    {
        Assert.Null(DetectionChooser.Pick(Array.Empty<Rect>(), mode, 0));
    }

    [Theory]
    [InlineData(DetectMode.Element, 0)]
    [InlineData(DetectMode.Element, 5)]
    [InlineData(DetectMode.Window, 0)]
    public void Pick_FromASingleEntryChain_ReturnsThatEntry(DetectMode mode, int level)
    {
        // A window whose UIA scan has not landed yet: the window rect is all there is.
        Assert.Equal(Window, DetectionChooser.Pick(new[] { Window }, mode, level));
    }

    // ---------------------------------------------------------------- Toggle ---

    [Fact]
    public void Toggle_SwitchesBetweenElementAndWindowDetection()
    {
        Assert.Equal(DetectMode.Window, DetectionChooser.Toggle(DetectMode.Element));
        Assert.Equal(DetectMode.Element, DetectionChooser.Toggle(DetectMode.Window));
    }

    // ---------------------------------------------------------------- Wheel ---

    [Fact]
    public void Wheel_InElementMode_WalksOutwardAndInward()
    {
        Assert.Equal((DetectMode.Element, 1), DetectionChooser.Wheel(DetectMode.Element, 0, 3, outward: true));
        Assert.Equal((DetectMode.Element, 0), DetectionChooser.Wheel(DetectMode.Element, 1, 3, outward: false));
    }

    [Fact]
    public void Wheel_InElementMode_StopsAtBothEndsOfTheChain()
    {
        Assert.Equal((DetectMode.Element, 2), DetectionChooser.Wheel(DetectMode.Element, 2, 3, outward: true));
        Assert.Equal((DetectMode.Element, 0), DetectionChooser.Wheel(DetectMode.Element, 0, 3, outward: false));
    }

    [Fact]
    public void Wheel_InWindowMode_SwitchesBackToElementDetection_FromTheWindowLevel()
    {
        // Snipaste: scrolling enters element detection. Starting from the window keeps the
        // highlight where it was, so the first inward step lands one level inside it.
        Assert.Equal((DetectMode.Element, 1), DetectionChooser.Wheel(DetectMode.Window, 0, 3, outward: false));
        Assert.Equal((DetectMode.Element, 2), DetectionChooser.Wheel(DetectMode.Window, 0, 3, outward: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Wheel_WithASingleCandidate_StaysAtLevelZero(int count)
    {
        Assert.Equal((DetectMode.Element, 0), DetectionChooser.Wheel(DetectMode.Element, 0, count, outward: true));
        Assert.Equal((DetectMode.Element, 0), DetectionChooser.Wheel(DetectMode.Window, 0, count, outward: false));
    }
}
