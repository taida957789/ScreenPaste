using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScreenPaste.Editor;
using ScreenPaste.Rendering;
using ScreenPaste.Settings;

namespace ScreenPaste.Capture;

/// <summary>
/// The magnifier ("local zoom") tool: frame an area of the capture, show it enlarged beside
/// itself, and let both the enlarged view and the framed source be moved and resized.
///
/// Split out of the main code-behind, which owns the selection, the other tools and the
/// toolbar. Everything that renders a magnifier lives in
/// <see cref="ScreenPaste.Rendering.MagnifyEffects"/>; this half is interaction only.
/// </summary>
public partial class CaptureOverlayWindow
{
    // Magnifier-annotation ("local zoom") settings + framing-drag state. NOTE: unrelated
    // to the _mag* fields below, which drive the pixel loupe that follows the cursor.
    private ShapeKind _magnifyShape = ShapeKind.RoundedRectangle;
    private double _magnifyZoom = 2.5;
    private double _magnifyBorderWidth = 3;
    private Color _magnifyBorderColor;
    private bool _magnifyConnector = true, _magnifyShadow = true, _magnifySmooth = true;
    private bool _magnifyWithAnnotations = true;
    private bool _magnifyDragging;
    private Point _magnifyStart;
    private Rectangle? _magnifyPreview;
    // Dragging a magnifier's framed source (re-points it at other content).
    private FrameworkElement? _magnifySourceDrag;
    private Point _magnifySourceGrab;
    private Rect _magnifySourceStart;
    // Resizing it via the 8 handles drawn on the frame while the magnifier is selected.
    private const int MinMagnifySourceSize = 6;
    private readonly List<Rectangle> _magnifySourceHandles = new();
    private int _magnifySourceHandleIndex = -1;
    private FrameworkElement? _magnifySourceResize;
    private Point _magnifySourceResizeGrab;
    private Rect _magnifySourceResizeStart;
    private Point _magnifySourceResizeViewStart;

    private Slider _magnifyZoomSlider = null!, _magnifyBorderSlider = null!;
    private StackPanel _magnifyOptionsPanel = null!;
    private readonly List<Button> _magnifyShapeButtons = new();
    private Button _magnifyConnectorButton = null!, _magnifyShadowButton = null!,
                   _magnifySmoothButton = null!, _magnifyAnnotationsButton = null!;

    // The composites a magnifier samples: everything under the magnifier layer (blur
    // included), or the bare screenshot when it was told to ignore other annotations.
    private BitmapSource? _magnifyBeneath;
    private BitmapSource? _regionCrop;

    /// <summary>The magnifier options row: shape, zoom, border, connector, shadow,
    /// smoothing, and whether the enlarged content includes the other annotations.</summary>
    private void BuildMagnifyOptions()
    {
        _magnifyOptionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _magnifyOptionsPanel.Children.Add(Label(Loc.T("lbl.shape")));
        _magnifyOptionsPanel.Children.Add(MakeMagnifyShapeButton(Loc.T("shape.rect"), ShapeKind.Rectangle));
        _magnifyOptionsPanel.Children.Add(MakeMagnifyShapeButton(Loc.T("shape.rounded"), ShapeKind.RoundedRectangle));
        _magnifyOptionsPanel.Children.Add(MakeMagnifyShapeButton(Loc.T("shape.ellipse"), ShapeKind.Ellipse));
        _magnifyOptionsPanel.Children.Add(Label(Loc.T("lbl.zoom")));
        _magnifyZoomSlider = new Slider
        {
            Minimum = MagnifyEffects.MinZoom,
            Maximum = MagnifyEffects.MaxZoom,
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center,
            Value = _magnifyZoom,
        };
        _magnifyZoomSlider.ValueChanged += (_, e) => _magnifyZoom = e.NewValue;
        _magnifyOptionsPanel.Children.Add(_magnifyZoomSlider);
        _magnifyOptionsPanel.Children.Add(ValueReadout(_magnifyZoomSlider, v => v.ToString("0.0") + "×"));
        _magnifyOptionsPanel.Children.Add(Label(Loc.T("lbl.border")));
        _magnifyBorderSlider = new Slider
        {
            Minimum = 0,
            Maximum = 12,
            Width = 70,
            VerticalAlignment = VerticalAlignment.Center,
            Value = _magnifyBorderWidth,
        };
        _magnifyBorderSlider.ValueChanged += (_, e) => _magnifyBorderWidth = e.NewValue;
        _magnifyOptionsPanel.Children.Add(_magnifyBorderSlider);
        _magnifyOptionsPanel.Children.Add(ValueReadout(_magnifyBorderSlider, v => v.ToString("0")));
        _magnifyConnectorButton = MakeSmallToggle(Loc.T("magnify.connector"));
        _magnifyConnectorButton.Click += (_, _) => { _magnifyConnector = !_magnifyConnector; RefreshMagnifyToggles(); };
        _magnifyShadowButton = MakeSmallToggle(Loc.T("magnify.shadow"));
        _magnifyShadowButton.Click += (_, _) => { _magnifyShadow = !_magnifyShadow; RefreshMagnifyToggles(); };
        _magnifySmoothButton = MakeSmallToggle(Loc.T("magnify.smooth"));
        _magnifySmoothButton.Click += (_, _) => { _magnifySmooth = !_magnifySmooth; RefreshMagnifyToggles(); };
        _magnifyAnnotationsButton = MakeSmallToggle(Loc.T("magnify.withAnnotations"));
        _magnifyAnnotationsButton.Click += (_, _) => { _magnifyWithAnnotations = !_magnifyWithAnnotations; RefreshMagnifyToggles(); };
        _magnifyOptionsPanel.Children.Add(_magnifyConnectorButton);
        _magnifyOptionsPanel.Children.Add(_magnifyShadowButton);
        _magnifyOptionsPanel.Children.Add(_magnifySmoothButton);
        _magnifyOptionsPanel.Children.Add(_magnifyAnnotationsButton);
        _magnifyOptionsPanel.Children.Add(Label(Loc.T("lbl.color")));
        AddColorSwatches(_magnifyOptionsPanel);
        RefreshMagnifyToggles();
        ToolbarStack.Children.Add(_magnifyOptionsPanel);
    }

    private Button MakeMagnifyShapeButton(string text, ShapeKind kind)
    {
        var b = MakeSmallToggle(text);
        b.Tag = kind;
        b.Click += (_, _) => SelectMagnifyShape(kind);
        _magnifyShapeButtons.Add(b);
        return b;
    }

    private void SelectMagnifyShape(ShapeKind kind)
    {
        _magnifyShape = kind;
        foreach (var b in _magnifyShapeButtons)
            b.Background = (ShapeKind)b.Tag! == kind ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private void RefreshMagnifyToggles()
    {
        _magnifyConnectorButton.Background = _magnifyConnector ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _magnifyShadowButton.Background = _magnifyShadow ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _magnifySmoothButton.Background = _magnifySmooth ? Theme.ActiveBrush : Theme.ButtonBgBrush;
        _magnifyAnnotationsButton.Background = _magnifyWithAnnotations ? Theme.ActiveBrush : Theme.ButtonBgBrush;
    }

    private BitmapSource MagnifyBeneath() => _magnifyBeneath ??=
        Compositor.ComposeBeneathMagnify(BlurBeneath(), _selection, BlurHost);

    /// <summary>The bare screenshot crop, for magnifiers told to ignore other annotations.</summary>
    private BitmapSource RegionCrop() => _regionCrop ??= Compositor.CropRegion(_screenshot, _selection);

    /// <summary>Re-sample every magnifier from the content it was told to enlarge.</summary>
    private void RefreshMagnifiers()
    {
        if (_phase != Phase.Editing || MagnifyHost.Children.Count == 0) return;

        foreach (var child in MagnifyHost.Children)
            if (child is FrameworkElement el)
                ResampleMagnifier(el);
    }

    private void ResampleMagnifier(FrameworkElement host)
    {
        if (MagnifyEffects.SpecOf(host) is not { } spec) return;   // skips the framing preview
        MagnifyEffects.Resample(host, spec.IncludeAnnotations ? MagnifyBeneath() : RegionCrop());
    }

    private void Magnify_MouseDown(Point start)
    {
        _magnifyDragging = true;
        _magnifyStart = start;
        _magnifyPreview = new Rectangle
        {
            Stroke = new SolidColorBrush(Theme.Accent),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x3D, 0xA9, 0xFC)),
        };
        Canvas.SetLeft(_magnifyPreview, start.X);
        Canvas.SetTop(_magnifyPreview, start.Y);
        MagnifyHost.Children.Add(_magnifyPreview);
        InteractionLayer.CaptureMouse();
    }

    private void Magnify_MouseMove(Point p)
    {
        if (_magnifyPreview == null) return;
        var r = MakeRect(_magnifyStart, p);
        Canvas.SetLeft(_magnifyPreview, r.X);
        Canvas.SetTop(_magnifyPreview, r.Y);
        _magnifyPreview.Width = r.Width;
        _magnifyPreview.Height = r.Height;
    }

    private void Magnify_MouseUp(Point p)
    {
        _magnifyDragging = false;
        InteractionLayer.ReleaseMouseCapture();

        if (_magnifyPreview != null) MagnifyHost.Children.Remove(_magnifyPreview);
        _magnifyPreview = null;

        var region = RegionBounds();
        var source = MakeRect(_magnifyStart, p);
        source.Intersect(region);
        if (source.Width < 6 || source.Height < 6) return;

        var spec = new MagnifySpec(source, _magnifyShape, _magnifyZoom, _magnifyBorderWidth,
            _magnifyBorderColor, _magnifyConnector, _magnifyShadow, _magnifySmooth,
            _magnifyWithAnnotations);
        var visual = MagnifyEffects.Create(spec, region);
        MagnifyHost.Children.Add(visual);   // the history push below re-samples it

        _history.Push(
            undo: () => MagnifyHost.Children.Remove(visual),
            redo: () => { if (!MagnifyHost.Children.Contains(visual)) MagnifyHost.Children.Add(visual); });
    }

    private void EnsureMagnifySourceHandles()
    {
        if (_magnifySourceHandles.Count > 0) return;
        Cursor[] cursors =
        {
            Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW,
            Cursors.SizeWE,                   Cursors.SizeWE,
            Cursors.SizeNESW, Cursors.SizeNS, Cursors.SizeNWSE,
        };
        for (int i = 0; i < 8; i++)
        {
            var h = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Theme.Accent),
                StrokeThickness = 2,
                Cursor = cursors[i],
                Tag = i,
            };
            h.MouseLeftButtonDown += MagnifySourceHandle_MouseDown;
            h.MouseMove += MagnifySourceHandle_MouseMove;
            h.MouseLeftButtonUp += MagnifySourceHandle_MouseUp;
            _magnifySourceHandles.Add(h);
            MagnifySourceHandleLayer.Children.Add(h);
        }
    }

    /// <summary>Park the handles on the selected magnifier's framed source, or hide them when
    /// the selection is not a magnifier. The source is region-local, hence the origin shift.</summary>
    private void LayoutMagnifySourceHandles()
    {
        var spec = _selected != null && _selected.Parent == MagnifyHost
            ? MagnifyEffects.SpecOf(_selected)
            : null;
        if (_phase != Phase.Editing || spec == null)
        {
            MagnifySourceHandleLayer.Visibility = Visibility.Collapsed;
            return;
        }

        EnsureMagnifySourceHandles();

        // outside: true — the handles ring the frame rather than straddling it, because a
        // small framed area (the whole point of a magnifier) would otherwise be papered over
        // by its own handles, leaving nothing to grab for moving it.
        var at = HandleGeometry.Place(
            new Rect(_selection.X + spec.Source.X, _selection.Y + spec.Source.Y,
                spec.Source.Width, spec.Source.Height),
            HandleSize, outside: true);
        for (int i = 0; i < HandleGeometry.Count; i++)
        {
            Canvas.SetLeft(_magnifySourceHandles[i], at[i].X);
            Canvas.SetTop(_magnifySourceHandles[i], at[i].Y);
        }
        MagnifySourceHandleLayer.Visibility = Visibility.Visible;
    }

    private void MagnifySourceHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle h || _phase != Phase.Editing) return;
        if (_selected == null || MagnifyEffects.SpecOf(_selected) is not { } spec) return;

        _magnifySourceHandleIndex = (int)h.Tag!;
        _magnifySourceResize = _selected;
        _magnifySourceResizeGrab = e.GetPosition(RootCanvas);
        _magnifySourceResizeStart = spec.Source;
        _magnifySourceResizeViewStart = MagnifyEffects.ViewPosition(_selected);
        h.CaptureMouse();
        e.Handled = true;
    }

    private void MagnifySourceHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_magnifySourceHandleIndex < 0 || _magnifySourceResize is not { } mag) return;
        var p = e.GetPosition(RootCanvas);

        MagnifyEffects.ResizeSource(mag, DragMagnifySourceEdges(
            _magnifySourceResizeStart, _magnifySourceHandleIndex,
            p.X - _magnifySourceResizeGrab.X, p.Y - _magnifySourceResizeGrab.Y));
        MagnifyEffects.ClampViewInto(mag, RegionBounds());
        ResampleMagnifier(mag);          // it now frames a different area
        MagnifyHost.UpdateLayout();      // the view resized; re-fit the marching box
        UpdateSelectionBox();
        LayoutMagnifySourceHandles();
    }

    /// <summary>The framed source after dragging one of its handles, held inside the
    /// selection. Tolerates a source that already sits outside the region.</summary>
    private Rect DragMagnifySourceEdges(Rect start, int index, double dx, double dy) =>
        HandleGeometry.DragEdges(start, index, dx, dy, MinMagnifySourceSize, RegionBounds());

    private void MagnifySourceHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_magnifySourceHandleIndex < 0) return;
        _magnifySourceHandleIndex = -1;
        (sender as Rectangle)?.ReleaseMouseCapture();
        e.Handled = true;

        var mag = _magnifySourceResize;
        _magnifySourceResize = null;
        if (mag == null || MagnifyEffects.SpecOf(mag) is not { } spec) return;

        var before = _magnifySourceResizeStart;
        var now = spec.Source;
        if (now == before) return;
        var viewBefore = _magnifySourceResizeViewStart;
        var viewNow = MagnifyEffects.ViewPosition(mag);

        // Restore the view position explicitly: the clamp above means re-deriving it from the
        // view centre would drift. The history's Changed hook re-samples for us.
        _history.Push(
            undo: () => { MagnifyEffects.SetFrame(mag, before, viewBefore); AfterMagnifyReframe(); },
            redo: () => { MagnifyEffects.SetFrame(mag, now, viewNow); AfterMagnifyReframe(); });
    }

    /// <summary>Undo/redo of a re-frame moves and resizes the view, so the chrome that tracks
    /// it has to catch up.</summary>
    private void AfterMagnifyReframe()
    {
        MagnifyHost.UpdateLayout();
        UpdateSelectionBox();
        LayoutMagnifySourceHandles();
    }

    /// <summary>Abort an in-flight framed-source move or resize, putting the frame back
    /// where the drag started. Returns false when nothing was being dragged.</summary>
    private bool CancelMagnifySourceDrag()
    {
        if (_magnifySourceResize is { } resizing)
        {
            _magnifySourceHandleIndex = -1;
            _magnifySourceResize = null;
            Mouse.Capture(null);   // capture is held by the handle, not by EditLayer
            MagnifyEffects.SetFrame(resizing, _magnifySourceResizeStart, _magnifySourceResizeViewStart);
            ResampleMagnifier(resizing);
            AfterMagnifyReframe();
            return true;
        }

        if (_magnifySourceDrag is { } moving)
        {
            _magnifySourceDrag = null;
            EditLayer.ReleaseMouseCapture();
            MagnifyEffects.MoveSourceTo(moving, _magnifySourceStart.TopLeft);
            ResampleMagnifier(moving);
            LayoutMagnifySourceHandles();
            return true;
        }

        return false;
    }

    /// <summary>Pull every magnifier's framed source back inside the current region.</summary>
    private void ClampMagnifySourcesIntoRegion()
    {
        var bounds = RegionBounds();
        foreach (var child in MagnifyHost.Children)
        {
            if (child is not FrameworkElement el || MagnifyEffects.SpecOf(el) is not { } spec) continue;
            var clamped = HandleGeometry.ClampInto(spec.Source, bounds, MinMagnifySourceSize);
            if (clamped != spec.Source) MagnifyEffects.ResizeSource(el, clamped);
        }
    }

    /// <summary>Topmost magnifier whose *framed source* contains <paramref name="p"/>
    /// (region-local coords, the same space the source rect is stored in).</summary>
    private FrameworkElement? HitMagnifySource(Point p)
    {
        for (int i = MagnifyHost.Children.Count - 1; i >= 0; i--)
            if (MagnifyHost.Children[i] is FrameworkElement el &&
                MagnifyEffects.SpecOf(el) is { } spec && spec.Source.Contains(p))
                return el;
        return null;
    }

    /// <summary>The selection in region-local coords — the box annotations live in.</summary>
    private Rect RegionBounds() => new(0, 0, _selection.Width, _selection.Height);

    /// <summary>Keep a framed source inside the selection, preserving its size.</summary>
    private Rect ClampSourceToRegion(Rect source) => new(
        Math.Clamp(source.X, 0, Math.Max(0, _selection.Width - source.Width)),
        Math.Clamp(source.Y, 0, Math.Max(0, _selection.Height - source.Height)),
        source.Width, source.Height);
}
