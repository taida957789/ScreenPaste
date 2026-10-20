using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenPaste.Rendering;

namespace ScreenPaste.Editor;

/// <summary>
/// Duplicates a capture annotation for Ctrl+Shift+C / Ctrl+Shift+V and Ctrl+drag: a new element
/// of the same kind that looks the same, at the original's place plus an offset. Knows every
/// kind the overlay puts in its hosts: rectangles and ellipses, lines and line strips, committed
/// text, pasted images, number markers, and blur and magnifier regions (rebuilt through their
/// spec helpers). The copy is detached; the caller adds it to a host, wires a sticker's mouse
/// handling, and re-samples a blur or magnifier.
/// </summary>
public static class AnnotationCloner
{
    /// <summary>A copy of <paramref name="element"/> moved by <paramref name="offset"/>, or null
    /// for an element it does not know. A magnifier's framed source moves along with its view;
    /// <paramref name="region"/> (region-local) is where a new one may place its view.</summary>
    public static FrameworkElement? Clone(FrameworkElement element, Vector offset, Rect region)
    {
        if (MagnifyEffects.SpecOf(element) is { } magnify) return CloneMagnifier(element, magnify, offset, region);

        FrameworkElement? copy = element switch
        {
            Canvas { Tag: BlurSpec blur } c => BlurEffects.Create(blur.Kind, new Rect(0, 0, c.Width, c.Height), blur.Strength),
            Grid g when NumberMarker.SpecOf(g) is { } number => NumberMarker.Create(number, default),
            Path p => ClonePath(p),
            Rectangle r => CloneShape(r, new Rectangle { RadiusX = r.RadiusX, RadiusY = r.RadiusY }),
            Ellipse e => CloneShape(e, new Ellipse()),
            TextBox t => CloneText(t),
            Image i => CloneImage(i),
            _ => null,
        };
        if (copy == null) return null;

        Canvas.SetLeft(copy, Canvas.GetLeft(element));
        Canvas.SetTop(copy, Canvas.GetTop(element));
        var (tx, ty) = element.RenderTransform is TranslateTransform tt ? (tt.X, tt.Y) : (0.0, 0.0);
        copy.RenderTransform = new TranslateTransform(tx + offset.X, ty + offset.Y);
        return copy;
    }

    private static FrameworkElement CloneMagnifier(FrameworkElement element, MagnifySpec spec, Vector offset, Rect region)
    {
        var source = Rect.Offset(spec.Source, offset);
        var copy = MagnifyEffects.Create(spec with { Source = source }, region);
        MagnifyEffects.SetFrame(copy, source, MagnifyEffects.ViewPosition(element) + offset);
        return copy;
    }

    private static Path ClonePath(Path p) => CopyStroke(p, new Path
    {
        Data = p.Data?.CloneCurrentValue(),
        StrokeStartLineCap = p.StrokeStartLineCap,
        StrokeEndLineCap = p.StrokeEndLineCap,
        StrokeLineJoin = p.StrokeLineJoin,
    });

    private static Shape CloneShape(Shape original, Shape copy)
    {
        copy.Width = original.Width;
        copy.Height = original.Height;
        return CopyStroke(original, copy);
    }

    private static T CopyStroke<T>(Shape original, T copy) where T : Shape
    {
        copy.Fill = original.Fill?.CloneCurrentValue();
        copy.Stroke = original.Stroke?.CloneCurrentValue();
        copy.StrokeThickness = original.StrokeThickness;
        return copy;
    }

    /// <summary>Committed text is a read-only, click-through text box (see CommitActiveText).</summary>
    private static TextBox CloneText(TextBox t) => new()
    {
        Text = t.Text,
        AcceptsReturn = t.AcceptsReturn,
        TextWrapping = t.TextWrapping,
        MinWidth = t.MinWidth,
        FontFamily = t.FontFamily,
        FontSize = t.FontSize,
        FontWeight = t.FontWeight,
        FontStyle = t.FontStyle,
        TextDecorations = t.TextDecorations?.CloneCurrentValue(),
        Foreground = t.Foreground?.CloneCurrentValue(),
        Background = t.Background?.CloneCurrentValue(),
        BorderBrush = t.BorderBrush?.CloneCurrentValue(),
        BorderThickness = t.BorderThickness,
        Padding = t.Padding,
        CaretBrush = t.CaretBrush?.CloneCurrentValue(),
        IsReadOnly = t.IsReadOnly,
        IsHitTestVisible = t.IsHitTestVisible,
        Focusable = t.Focusable,
    };

    private static Image CloneImage(Image i)
    {
        var copy = new Image { Source = i.Source, Width = i.Width, Height = i.Height, Stretch = i.Stretch };
        RenderOptions.SetBitmapScalingMode(copy, RenderOptions.GetBitmapScalingMode(i));
        return copy;
    }
}
