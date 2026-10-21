using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScreenPaste.Editor;
using ScreenPaste.Rendering;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// Ctrl+Shift+C / Ctrl+Shift+V and Ctrl+drag duplicate an annotation: the copy is a new
/// element of the same kind, looking the same, placed where the original is plus an offset.
/// WPF elements need an STA thread, hence <see cref="StaRunner"/>.
/// </summary>
public sealed class AnnotationClonerTests
{
    private static readonly Rect Region = new(0, 0, 600, 400);
    private static readonly Vector Offset = new(10, 10);

    private static TranslateTransform TranslateOf(UIElement el) => Assert.IsType<TranslateTransform>(el.RenderTransform);

    [Fact]
    public void Clone_ARectangle_CopiesItsLookAndPlace_MovedByTheOffset()
    {
        StaRunner.Run(() =>
        {
            // Arrange
            var original = new Rectangle
            {
                Width = 40, Height = 30, RadiusX = 5, RadiusY = 5,
                Fill = Brushes.Transparent, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 3,
            };
            Canvas.SetLeft(original, 12);
            Canvas.SetTop(original, 8);
            original.RenderTransform = new TranslateTransform(2, 3);

            // Act
            var copy = Assert.IsType<Rectangle>(AnnotationCloner.Clone(original, Offset, Region));

            // Assert
            Assert.NotSame(original, copy);
            Assert.Equal(40, copy.Width);
            Assert.Equal(30, copy.Height);
            Assert.Equal(5, copy.RadiusX);
            Assert.Equal(3, copy.StrokeThickness);
            Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(copy.Stroke).Color);
            Assert.Equal(12, Canvas.GetLeft(copy));
            Assert.Equal(8, Canvas.GetTop(copy));
            Assert.Equal(12, TranslateOf(copy).X);
            Assert.Equal(13, TranslateOf(copy).Y);
            Assert.Equal(2, TranslateOf(original).X);   // the original stays where it was
        });
    }

    [Fact]
    public void Clone_AnEllipse_StaysAnEllipse()
    {
        StaRunner.Run(() =>
        {
            var original = new Ellipse { Width = 20, Height = 20, Fill = new SolidColorBrush(Colors.Blue) };

            var copy = Assert.IsType<Ellipse>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(copy.Fill).Color);
            Assert.Equal(10, TranslateOf(copy).X);
        });
    }

    [Fact]
    public void Clone_ALine_GetsItsOwnCopyOfTheGeometry()
    {
        StaRunner.Run(() =>
        {
            var brush = new SolidColorBrush(Colors.Red);
            var original = new Path
            {
                Data = ArrowGeometry.Build(new Point(10, 10), new Point(90, 40), 3, false, true),
                Stroke = brush, Fill = brush, StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            };

            var copy = Assert.IsType<Path>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.NotSame(original.Data, copy.Data);
            Assert.Equal(original.Data.Bounds, copy.Data!.Bounds);
            Assert.Equal(3, copy.StrokeThickness);
            Assert.Equal(PenLineCap.Round, copy.StrokeEndLineCap);
            Assert.Equal(new Vector(10, 10), new Vector(TranslateOf(copy).X, TranslateOf(copy).Y));
        });
    }

    [Fact]
    public void Clone_CommittedText_CopiesTheTextAndStyle_AndStaysALabel()
    {
        StaRunner.Run(() =>
        {
            var original = new TextBox
            {
                Text = "標註 note", FontSize = 24, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Red),
                IsReadOnly = true, IsHitTestVisible = false, Focusable = false,
            };
            Canvas.SetLeft(original, 30);
            Canvas.SetTop(original, 40);

            var copy = Assert.IsType<TextBox>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.Equal("標註 note", copy.Text);
            Assert.Equal(24, copy.FontSize);
            Assert.Equal(FontWeights.Bold, copy.FontWeight);
            Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(copy.Foreground).Color);
            Assert.True(copy.IsReadOnly);
            Assert.False(copy.IsHitTestVisible);
            Assert.False(copy.Focusable);
            Assert.Equal(30, Canvas.GetLeft(copy));
        });
    }

    [Fact]
    public void Clone_ASticker_SharesTheImage()
    {
        StaRunner.Run(() =>
        {
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            var original = new Image { Source = bitmap, Width = 50, Height = 60, Stretch = Stretch.Fill };

            var copy = Assert.IsType<Image>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.Same(bitmap, copy.Source);
            Assert.Equal(50, copy.Width);
            Assert.Equal(60, copy.Height);
            Assert.Equal(Stretch.Fill, copy.Stretch);
        });
    }

    [Fact]
    public void Clone_ABlurRegion_KeepsItsKindStrengthAndSize()
    {
        StaRunner.Run(() =>
        {
            var original = BlurEffects.Create(BlurKind.Mosaic, new Rect(5, 6, 50, 40), 12);

            var copy = Assert.IsAssignableFrom<FrameworkElement>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.Equal(new BlurSpec(BlurKind.Mosaic, 12), copy.Tag);
            Assert.Equal(5, Canvas.GetLeft(copy));
            Assert.Equal(6, Canvas.GetTop(copy));
            Assert.Equal(50, copy.Width);
            Assert.Equal(40, copy.Height);
            Assert.Equal(10, TranslateOf(copy).X);
        });
    }

    [Fact]
    public void Clone_AMagnifier_MovesBothItsFramedSourceAndItsView()
    {
        StaRunner.Run(() =>
        {
            var spec = new MagnifySpec(new Rect(40, 60, 70, 34), ShapeKind.Ellipse, 2.5, 3, Colors.Red,
                Connector: true, Shadow: false, Smooth: true, IncludeAnnotations: true);
            var original = MagnifyEffects.Create(spec, Region);
            var view = MagnifyEffects.ViewPosition(original);

            var copy = Assert.IsAssignableFrom<FrameworkElement>(AnnotationCloner.Clone(original, Offset, Region));

            var copied = Assert.IsType<MagnifySpec>(MagnifyEffects.SpecOf(copy));
            Assert.Equal(new Rect(50, 70, 70, 34), copied.Source);
            Assert.Equal(spec with { Source = copied.Source }, copied);
            Assert.Equal(view + Offset, MagnifyEffects.ViewPosition(copy));
            Assert.Equal(spec.Source, MagnifyEffects.SpecOf(original)!.Source);
        });
    }

    [Fact]
    public void Clone_ANumberMarker_KeepsItsNumberAndLook()
    {
        StaRunner.Run(() =>
        {
            var spec = new NumberMarkerSpec(4, 28, Colors.Red);
            var original = NumberMarker.Create(spec, new Point(100, 50));

            var copy = Assert.IsAssignableFrom<FrameworkElement>(AnnotationCloner.Clone(original, Offset, Region));

            Assert.Equal(spec, NumberMarker.SpecOf(copy));
            Assert.Equal(Canvas.GetLeft(original), Canvas.GetLeft(copy));
            Assert.Equal(10, TranslateOf(copy).Y);
        });
    }

    [Fact]
    public void Clone_WithNoOffset_LandsOnTheOriginal()
    {
        StaRunner.Run(() =>
        {
            var original = new Rectangle { Width = 40, Height = 30 };
            original.RenderTransform = new TranslateTransform(7, -4);

            var copy = Assert.IsType<Rectangle>(AnnotationCloner.Clone(original, default, Region));

            Assert.Equal(7, TranslateOf(copy).X);
            Assert.Equal(-4, TranslateOf(copy).Y);
            Assert.NotSame(original.RenderTransform, copy.RenderTransform);
        });
    }

    [Fact]
    public void Clone_AnElementItDoesNotKnow_IsNothing()
    {
        StaRunner.Run(() =>
        {
            Assert.Null(AnnotationCloner.Clone(new Border(), Offset, Region));
        });
    }
}
