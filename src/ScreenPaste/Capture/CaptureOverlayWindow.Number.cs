using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenPaste.Editor;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The number-marker tool (Ctrl+7): a click places a filled circle with the next number, in
/// the shape colour. + and - (typed characters, so any layout works) or the toolbar's buttons
/// change the next number, never below 1, and the wheel, 1 / 2 or [ / ] its size. Undoing a
/// marker gives its number back (<see cref="NumberSequence"/>); clearing everything or framing
/// a new area starts again from 1.
/// </summary>
public partial class CaptureOverlayWindow
{
    private const string GlyphNumber = "①";   // circled digit one

    private double _numberSize = 28;
    private NumberSequence _numbers = NumberSequence.Start;
    private Slider _numberSizeSlider = null!;
    private StackPanel _numberOptionsPanel = null!;
    private TextBlock? _nextNumberText;              // built with the toolbar

    private void InitNumber()
    {
        var range = PenSizeStep.RangeOf(ToolKind.Number)!.Value;
        _numberSize = Math.Clamp(_settings.NumberSize, range.Min, range.Max);
    }

    /// <summary>The toolbar button: the glyph comes from a text font, not the icon font.</summary>
    private Button MakeNumberToolButton()
    {
        var b = MakeToolButton(GlyphNumber, ToolKind.Number);
        if (b.Content is TextBlock glyph)
        {
            glyph.FontFamily = new FontFamily("Segoe UI Symbol");
            glyph.FontSize = 18;
        }
        return b;
    }

    /// <summary>The number options row: circle size, the next number with - / + buttons, and
    /// the colour (the shape tool's).</summary>
    private void BuildNumberOptions()
    {
        var range = PenSizeStep.RangeOf(ToolKind.Number)!.Value;
        _numberOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _numberOptionsPanel.Children.Add(Label(Loc.T("lbl.size")));
        _numberSizeSlider = new Slider
        {
            Minimum = range.Min,
            Maximum = range.Max,
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center,
            Value = _numberSize,
        };
        _numberSizeSlider.ValueChanged += (_, e) => _numberSize = e.NewValue;
        _numberOptionsPanel.Children.Add(_numberSizeSlider);
        _numberOptionsPanel.Children.Add(ValueReadout(_numberSizeSlider, v => v.ToString("0")));

        _numberOptionsPanel.Children.Add(Label(Loc.T("number.next")));
        _numberOptionsPanel.Children.Add(MakeNumberStepButton("−", -1));   // minus sign
        _nextNumberText = new TextBlock
        {
            Foreground = Theme.ForegroundBrush,
            FontSize = 12,
            MinWidth = 24,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _numberOptionsPanel.Children.Add(_nextNumberText);
        _numberOptionsPanel.Children.Add(MakeNumberStepButton("+", 1));
        RefreshNextNumber();

        _numberOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_numberOptionsPanel);
        ToolbarStack.Children.Add(_numberOptionsPanel);
    }

    private Button MakeNumberStepButton(string text, int delta)
    {
        var b = MakeSmallToggle(text);
        b.ToolTip = Loc.T("number.hint");
        b.Click += (_, _) => StepNextNumber(delta);
        return b;
    }

    /// <summary>A click with the number tool: the next number's marker, centred on
    /// <paramref name="at"/> (region-local). One undo step, which also gives the number back.</summary>
    private void PlaceNumber(Point at)
    {
        int number = _numbers.Next;
        var marker = NumberMarker.Create(new NumberMarkerSpec(number, _numberSize, _shapeColor), at);
        ShapeHost.Children.Add(marker);
        SetNumbers(_numbers.AfterPlacing());
        RecordAdded(ShapeHost, marker,
            undone: () => SetNumbers(_numbers.AfterUndoing(number)),
            redone: () => SetNumbers(_numbers.AfterRedoing(number)));
    }

    /// <summary>+ / -: the number the next click places; the size chip says it, since the
    /// toolbar may be hidden.</summary>
    private void StepNextNumber(int delta)
    {
        SetNumbers(_numbers.Adjusted(delta));
        FlashSizeNote($"{Loc.T("number.next")} {_numbers.Next}");
    }

    private void SetNumbers(NumberSequence numbers)
    {
        _numbers = numbers;
        RefreshNextNumber();
    }

    private void RefreshNextNumber()
    {
        if (_nextNumberText != null) _nextNumberText.Text = _numbers.Next.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
